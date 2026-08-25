"""In-process authority for freshly collected .NET compatibility evidence.

Files supplied through ``--evidence-results`` are intentionally never trusted.
This module is the only authority boundary: the parent prepares a hash-bound
request, launches this exact tracked file as an isolated standard-library child,
independently validates the signed child receipt, and seals the resulting
``EvidenceResults`` only in memory.

Evidence tests emit one JSON record per executed xUnit case into the directory
named by ``GONIEGONIE_EVIDENCE_RECORDS_DIRECTORY``.  A record has this exact
shape (``output`` may be any finite JSON value)::

    {
      "assertion_id": "service-run-parity",
      "exercised_load": "not_applicable",
      "output": {"value": 1},
      "schema": "goniegonie.trusted-evidence-record.v1",
      "session_nonce": "...",
      "structural_only": false,
      "test_case": "ServiceParityTests.RunMatchesUpstream"
    }

The collector matches ``test_case`` to the exact TRX test name and hashes the
canonical ordered case/output set.  A structural-only record, missing case,
ambiguous theory case, foreign codeBase, stale source, dirty repository, or
unlocked restore fails closed before authority is granted.
"""

from __future__ import annotations

import base64
from dataclasses import dataclass, field
import hashlib
import hmac
import json
import os
from pathlib import Path, PurePosixPath
import re
import secrets
import shutil
import stat
import subprocess
import sys
import types
from typing import Any, Callable, Mapping, Sequence
import uuid
import weakref
import xml.etree.ElementTree as ElementTree

if __package__:
    from .errors import ConfigurationError
    from .evidence import (
        EvidenceReceipt,
        EvidenceResults,
        ExecutedAssertion,
        SymbolEvidenceRegistry,
    )
else:  # The isolated child deliberately has no repository package on sys.path.
    class ConfigurationError(RuntimeError):
        pass

    EvidenceReceipt = Any
    EvidenceResults = object
    ExecutedAssertion = Any
    SymbolEvidenceRegistry = Any


REQUEST_SCHEMA = "goniegonie.trusted-evidence-request.v1"
CHILD_RESULT_SCHEMA = "goniegonie.trusted-evidence-child-result.v1"
RECORD_SCHEMA = "goniegonie.trusted-evidence-record.v1"
AUTHORITY_RECEIPT_SCHEMA = "goniegonie.trusted-evidence-authority-receipt.v1"
ARTIFACT_INDEX_SCHEMA = "goniegonie.trusted-evidence-artifact-index.v1"
SDK_MANIFEST_SCHEMA = "goniegonie.trusted-dotnet-sdk-manifest.v1"
EVALUATED_GRAPH_SCHEMA = "goniegonie.trusted-msbuild-evaluated-graph.v1"
_INVENTORY_SCHEMA = "goniegonie.upstream-public-symbol-inventory.v2"
_EVIDENCE_SCHEMA = "goniegonie.upstream-symbol-evidence.v1"
_MATRIX_SCHEMA = "goniegonie.upstream-compatibility-matrix.v1"

_SHA256 = re.compile(r"^sha256:[0-9a-f]{64}$")
_COMMIT = re.compile(r"^[0-9a-f]{40}(?:[0-9a-f]{24})?$")
_IDENTIFIER = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
_SDK_VERSION = re.compile(r"^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$")
_TRX_NAMESPACE = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
_LOAD_CASES = frozenset({"not_applicable", "zero", "nonzero"})
_WINDOWS_INVALID_PATH_CHARACTERS = frozenset('<>:"|?*')
_WINDOWS_RESERVED_PATH_PARTS = frozenset(
    {"aux", "con", "nul", "prn"}
    | {f"com{index}" for index in range(1, 10)}
    | {f"lpt{index}" for index in range(1, 10)}
)
_CHILD_SECRET_BYTES = 32
_MAX_CHILD_OUTPUT_BYTES = 16 * 1024 * 1024
_MAX_RECORD_BYTES = 8 * 1024 * 1024
_MAX_TRX_BYTES = 128 * 1024 * 1024
_COLLECTOR_PATH = "tools/upstream-tracker/goniegonie_upstream_tracker/trusted_collector.py"
_COLLECTOR_SYMBOL = "collect_trusted_evidence"


class TrustedCollectorError(ConfigurationError):
    """A trusted collection session could not be proven exact."""


@dataclass(frozen=True)
class TrustedEvidenceResults(EvidenceResults):
    """Evidence results carrying a process-local, non-serializable authority seal."""

    authority_receipt_sha256: str
    authority_receipt_path: str
    artifact_index_sha256: str
    artifact_index_path: str
    session_id: str
    project_count: int
    assertion_count: int
    artifact_count: int
    _authority_mac: str = field(repr=False, compare=False)


def _create_authority_boundary(
    exact_collector: Callable[..., tuple[EvidenceResults, Mapping[str, Any]]],
):
    """Expose only one exact collect-and-seal path plus read-only validation.

    Python code executing in this process is already inside the trust boundary,
    so this is not a sandbox. Constructors, subclasses, copied MAC fields, and
    synthetic child receipts cannot manufacture authority: the only issuer
    calls the non-injectable exact child-launch path itself before registering
    the returned object identity.
    """

    key = secrets.token_bytes(32)
    live: dict[int, tuple[weakref.ReferenceType[object], str, str]] = {}

    def collect_and_seal(*args: Any, **kwargs: Any) -> TrustedEvidenceResults:
        results, trace = exact_collector(*args, **kwargs)
        if not results.assertions:
            raise TrustedCollectorError(
                "trusted authority cannot seal an empty assertion result"
            )
        for name in ("project_count", "assertion_count", "artifact_count"):
            count = trace.get(name)
            if not isinstance(count, int) or isinstance(count, bool) or count <= 0:
                raise TrustedCollectorError(f"trusted authority {name} is invalid")
        if trace["assertion_count"] != len(results.assertions):
            raise TrustedCollectorError(
                "trusted authority assertion_count does not match its results"
            )
        payload = _authority_seal_payload(results.content_sha256, trace)
        mac = _hmac_sha256(key, payload)
        sealed = TrustedEvidenceResults(
            results.upstream_commit,
            results.inventory_sha256,
            results.symbol_evidence_sha256,
            results.collector_path,
            results.collector_symbol,
            results.collector_source_sha256,
            results.assertions,
            results.target_framework,
            trace["authority_receipt_sha256"],
            trace["authority_receipt_path"],
            trace["artifact_index_sha256"],
            trace["artifact_index_path"],
            trace["session_id"],
            trace["project_count"],
            trace["assertion_count"],
            trace["artifact_count"],
            mac,
        )
        fingerprint = _sha256_data(
            {
                "authority_mac": mac,
                "artifact_index_path": sealed.artifact_index_path,
                "artifact_index_sha256": sealed.artifact_index_sha256,
                "artifact_count": sealed.artifact_count,
                "assertion_count": sealed.assertion_count,
                "content_sha256": sealed.content_sha256,
                "project_count": sealed.project_count,
                "receipt_path": sealed.authority_receipt_path,
                "receipt_sha256": sealed.authority_receipt_sha256,
                "session_id": sealed.session_id,
            }
        )
        identifier = id(sealed)

        def remove(reference: weakref.ReferenceType[object]) -> None:
            current = live.get(identifier)
            if current is not None and current[0] is reference:
                live.pop(identifier, None)

        live[identifier] = (weakref.ref(sealed, remove), fingerprint, mac)
        return sealed

    def validate(value: object) -> bool:
        if type(value) is not TrustedEvidenceResults:
            return False
        entry = live.get(id(value))
        if entry is None or entry[0]() is not value:
            return False
        assert isinstance(value, TrustedEvidenceResults)
        expected_mac = _hmac_sha256(
            key,
            _authority_seal_payload(value.content_sha256, _authority_trace_data(value)),
        )
        fingerprint = _sha256_data(
            {
                "authority_mac": value._authority_mac,
                "artifact_index_path": value.artifact_index_path,
                "artifact_index_sha256": value.artifact_index_sha256,
                "artifact_count": value.artifact_count,
                "assertion_count": value.assertion_count,
                "content_sha256": value.content_sha256,
                "project_count": value.project_count,
                "receipt_path": value.authority_receipt_path,
                "receipt_sha256": value.authority_receipt_sha256,
                "session_id": value.session_id,
            }
        )
        return (
            hmac.compare_digest(value._authority_mac, expected_mac)
            and hmac.compare_digest(value._authority_mac, entry[2])
            and hmac.compare_digest(fingerprint, entry[1])
        )

    return collect_and_seal, validate


def is_authoritative_evidence_results(value: object) -> bool:
    """Return true only for an untampered result sealed by this process."""

    try:
        return _validate_live_authority(value)
    except (AttributeError, TypeError, ValueError):
        return False


def authority_receipt_sha256(value: object) -> str | None:
    """Expose the audit receipt hash only after the in-memory seal validates."""

    if not is_authoritative_evidence_results(value):
        return None
    assert isinstance(value, TrustedEvidenceResults)
    return value.authority_receipt_sha256


def collect_trusted_evidence(
    repository_root: Path,
    inventory: Any,
    symbol_evidence: SymbolEvidenceRegistry,
    required_assertion_ids: Sequence[str],
    *,
    sessions_root: Path | None = None,
    target_framework: str = "net8.0-windows",
    timeout_seconds: int = 1800,
) -> TrustedEvidenceResults:
    required = tuple(required_assertion_ids)
    if not required:
        raise TrustedCollectorError(
            "trusted evidence collection requires at least one assertion"
        )
    return _collect_and_seal(
        repository_root,
        inventory,
        symbol_evidence,
        required,
        sessions_root=sessions_root,
        target_framework=target_framework,
        timeout_seconds=timeout_seconds,
    )


def _collect_unsealed_evidence(
    repository_root: Path,
    inventory: Any,
    symbol_evidence: SymbolEvidenceRegistry,
    required_assertion_ids: Sequence[str],
    *,
    sessions_root: Path | None = None,
    target_framework: str = "net8.0-windows",
    timeout_seconds: int = 1800,
) -> tuple[EvidenceResults, Mapping[str, Any]]:
    """Run required exact assertions in a fresh child and return sealed evidence.

    The canonical inventory and symbol-evidence manifests must be byte-exact
    clean HEAD files.  The function deliberately offers no executable/runner
    override: monkey-patchable test seams stay private and cannot be selected by
    the CLI or a user-supplied evidence artifact.
    """

    root = repository_root.resolve(strict=True)
    _require_directory(root, "repository root")
    session_parent = (
        root / "temp" / "u"
        if sessions_root is None
        else sessions_root.resolve(strict=False)
    )
    expected_parent = root / "temp" / "u"
    if session_parent != expected_parent:
        raise TrustedCollectorError(
            "trusted evidence sessions must be beneath temp/u"
        )
    _require_safe_ancestors(root, session_parent.parent)
    session_parent.mkdir(parents=True, exist_ok=True)
    _require_safe_ancestors(root, session_parent)

    required = tuple(required_assertion_ids)
    if not required:
        raise TrustedCollectorError(
            "trusted evidence collection requires at least one assertion"
        )
    if required != tuple(sorted(set(required))):
        raise TrustedCollectorError(
            "trusted evidence assertion ids must be unique and sorted"
        )
    git = _resolve_git()
    git_hash = _sha256_file(git)
    snapshot = _exact_repository_snapshot(root, git, git_hash)
    _require_canonical_manifest(
        root / "upstream" / "public-symbol-inventory.json",
        inventory.to_data(),
        "public symbol inventory",
    )
    _require_canonical_manifest(
        root / "upstream" / "symbol-evidence.json",
        symbol_evidence.to_data(),
        "symbol evidence registry",
    )
    (
        canonical_inventory,
        canonical_evidence,
        canonical_matrix,
        canonical_required,
    ) = _load_canonical_evidence_manifests(root)
    if required != canonical_required:
        raise TrustedCollectorError(
            "caller-required assertions differ from the canonical matrix/evidence closure"
        )
    session_id = uuid.uuid4().hex
    session = session_parent / session_id
    try:
        session.mkdir(mode=0o700)
    except FileExistsError as exception:
        raise TrustedCollectorError("trusted evidence session collision") from exception
    _require_safe_ancestors(root, session)
    sdk = _load_pinned_sdk(root / "global.json")
    dotnet, sdk_root = _resolve_dotnet(
        root,
        sdk,
        session / "e" / "d0",
    )
    sdk_manifest = _sdk_toolchain_manifest(sdk_root)
    source_root = session / "s"
    source_tree = _materialize_source_tree(root, source_root, snapshot[1])
    _verify_msbuild_xml_sources(source_root, source_tree)
    projects, input_paths = _build_collection_plan(
        source_root,
        session,
        canonical_evidence,
        required,
        target_framework,
        snapshot[1],
        source_tree,
        dotnet,
        sdk_manifest,
    )
    input_paths.update(
        {
            "global.json",
            "upstream/public-symbol-inventory.json",
            "upstream/symbol-evidence.json",
            "upstream/compatibility-matrix.json",
            _COLLECTOR_PATH,
        }
    )
    inputs = tuple(
        {"path": path, "sha256": _hash_repository_file(root, path)}
        for path in sorted(input_paths)
    )
    package_locks = tuple(
        item for item in inputs if PurePosixPath(item["path"]).name == "packages.lock.json"
    )

    nonce = secrets.token_hex(32)
    request: dict[str, Any] = {
        "assertion_count": len(required),
        "dotnet": {
            "path": dotnet.as_posix(),
            "sdk_manifest": sdk_manifest,
            "sha256": _sha256_file(dotnet),
            "sdk_root": sdk_root.as_posix(),
            "sdk_version": sdk,
        },
        "evidence_binding": {
            "collector_path": _COLLECTOR_PATH,
            "collector_source_sha256": _hash_repository_file(root, _COLLECTOR_PATH),
            "collector_symbol": _COLLECTOR_SYMBOL,
            "inventory_sha256": canonical_inventory["content_sha256"],
            "matrix_sha256": canonical_matrix["content_sha256"],
            "symbol_evidence_sha256": canonical_evidence["content_sha256"],
            "upstream_commit": canonical_inventory["upstream_commit"],
        },
        "git": {
            "path": git.as_posix(),
            "sha256": git_hash,
        },
        "inputs": list(inputs),
        "nonce": nonce,
        "package_locks": list(package_locks),
        "project_count": len(projects),
        "projects": list(projects),
        "repository_head": snapshot[0],
        "repository_root": root.as_posix(),
        "required_assertion_ids": list(required),
        "schema": REQUEST_SCHEMA,
        "session_directory": session.as_posix(),
        "session_id": session_id,
        "source": {
            **source_tree,
            "root": source_root.as_posix(),
        },
        "target_framework": target_framework,
    }
    request["projects"] = [
        {
            **project,
            "arguments": _dotnet_test_command(
                request,
                project,
                session / "p" / project["slug"] / "b",
                session / "p" / project["slug"] / "o",
                session / "p" / project["slug"] / "t",
            ),
            "restore_arguments": _dotnet_restore_command(request, project),
        }
        for project in request["projects"]
    ]
    _verify_canonical_evidence_binding(source_root, request["evidence_binding"])
    secret = secrets.token_bytes(_CHILD_SECRET_BYTES)
    request_bytes = _canonical_json_bytes(request)
    request_path = session / "q.json"
    _write_exclusive(request_path, request_bytes)
    request_artifact = _session_artifact(session, request_path)
    envelope = {
        "request": request,
        "request_hmac": _hmac_sha256(secret, request_bytes),
        "secret": base64.b64encode(secret).decode("ascii"),
    }
    child_result = _launch_isolated_child(
        root,
        envelope,
        timeout_seconds=timeout_seconds,
    )
    verified = _validate_child_result_artifacts(request, child_result, secret)
    child_result_path = session / "z.json"
    _write_exclusive(child_result_path, _canonical_json_bytes(verified))
    child_result_artifact = _session_artifact(session, child_result_path)

    after = _exact_repository_snapshot(root, git, git_hash)
    if after != snapshot:
        raise TrustedCollectorError(
            "repository state changed during trusted evidence collection"
        )
    _verify_materialized_source(source_root, source_tree)
    assertions = tuple(
        _executed_assertion_from_child(item)
        for item in verified["assertions"]
    )
    base = EvidenceResults(
        canonical_inventory["upstream_commit"],
        canonical_inventory["content_sha256"],
        canonical_evidence["content_sha256"],
        _COLLECTOR_PATH,
        _COLLECTOR_SYMBOL,
        _hash_repository_file(root, _COLLECTOR_PATH),
        assertions,
        target_framework,
    )
    normalized_request = _validate_request(request)
    if _expected_results_binding(normalized_request, verified) != (
        _evidence_results_authority_binding(base)
    ):
        raise TrustedCollectorError(
            "validated assertions do not match their canonical evidence bindings"
        )
    artifact_index = _build_session_artifact_index(
        request,
        verified,
        request_artifact,
        child_result_artifact,
    )
    artifact_index_path = session / "i.json"
    _write_exclusive(artifact_index_path, _canonical_json_bytes(artifact_index))
    artifact_index_artifact = _session_artifact(session, artifact_index_path)
    project_count = len(request["projects"])
    assertion_count = len(verified["assertions"])
    artifact_count = len(artifact_index["artifacts"])
    if (
        project_count <= 0
        or assertion_count <= 0
        or artifact_count <= 0
        or project_count != artifact_index["project_count"]
        or assertion_count != artifact_index["assertion_count"]
        or artifact_count != artifact_index["artifact_count"]
    ):
        raise TrustedCollectorError("trusted authority session counts are inconsistent")
    authority_receipt = {
        "artifact_count": artifact_count,
        "artifact_index_path": "i.json",
        "artifact_index_sha256": artifact_index_artifact["sha256"],
        "assertion_count": assertion_count,
        "child_result_sha256": child_result_artifact["sha256"],
        "collector_source_sha256": request["evidence_binding"]["collector_source_sha256"],
        "dotnet_executable_sha256": request["dotnet"]["sha256"],
        "evidence_results_sha256": base.content_sha256,
        "git_executable_sha256": request["git"]["sha256"],
        "inventory_sha256": request["evidence_binding"]["inventory_sha256"],
        "matrix_sha256": request["evidence_binding"]["matrix_sha256"],
        "project_count": project_count,
        "repository_head": snapshot[0],
        "request_sha256": _sha256_bytes(request_bytes),
        "schema": AUTHORITY_RECEIPT_SCHEMA,
        "session_id": session_id,
        "source_tree_sha256": source_tree["sha256"],
        "symbol_evidence_sha256": request["evidence_binding"]["symbol_evidence_sha256"],
        "target_framework": target_framework,
        "toolchain_manifest_sha256": sdk_manifest["sha256"],
        "upstream_commit": request["evidence_binding"]["upstream_commit"],
    }
    authority_receipt_path = session / "a.json"
    _write_exclusive(
        authority_receipt_path,
        _canonical_json_bytes(authority_receipt),
    )
    authority_receipt_artifact = _session_artifact(session, authority_receipt_path)
    trace = {
        "artifact_index_path": artifact_index_path.relative_to(root).as_posix(),
        "artifact_index_sha256": artifact_index_artifact["sha256"],
        "authority_receipt_path": authority_receipt_path.relative_to(root).as_posix(),
        "authority_receipt_sha256": authority_receipt_artifact["sha256"],
        "artifact_count": artifact_count,
        "assertion_count": assertion_count,
        "project_count": project_count,
        "session_id": session_id,
    }
    return base, trace


def _authority_trace_data(value: TrustedEvidenceResults) -> Mapping[str, Any]:
    return {
        "artifact_index_path": value.artifact_index_path,
        "artifact_index_sha256": value.artifact_index_sha256,
        "authority_receipt_path": value.authority_receipt_path,
        "authority_receipt_sha256": value.authority_receipt_sha256,
        "artifact_count": value.artifact_count,
        "assertion_count": value.assertion_count,
        "project_count": value.project_count,
        "session_id": value.session_id,
    }


def _authority_seal_payload(
    evidence_hash: str,
    trace: Mapping[str, Any],
) -> bytes:
    return _canonical_json_bytes(
        {
            "artifact_index_path": trace["artifact_index_path"],
            "artifact_index_sha256": trace["artifact_index_sha256"],
            "authority_receipt_path": trace["authority_receipt_path"],
            "artifact_count": trace["artifact_count"],
            "assertion_count": trace["assertion_count"],
            "evidence_results_sha256": evidence_hash,
            "project_count": trace["project_count"],
            "receipt_sha256": trace["authority_receipt_sha256"],
            "session_id": trace["session_id"],
        }
    )


def _expected_results_binding(
    request: Mapping[str, Any],
    payload: Mapping[str, Any],
) -> str:
    binding = request["evidence_binding"]
    return _sha256_data(
        {
            "assertions": payload["assertions"],
            "collector": {
                "path": binding["collector_path"],
                "source_sha256": binding["collector_source_sha256"],
                "symbol": binding["collector_symbol"],
            },
            "inventory_sha256": binding["inventory_sha256"],
            "symbol_evidence_sha256": binding["symbol_evidence_sha256"],
            "target_framework": request["target_framework"],
            "upstream_commit": binding["upstream_commit"],
        }
    )


def _evidence_results_authority_binding(results: EvidenceResults) -> str:
    return _sha256_data(
        {
            "assertions": [item.to_data() for item in results.assertions],
            "collector": {
                "path": results.collector_path,
                "source_sha256": results.collector_source_sha256,
                "symbol": results.collector_symbol,
            },
            "inventory_sha256": results.inventory_sha256,
            "symbol_evidence_sha256": results.symbol_evidence_sha256,
            "target_framework": results.target_framework,
            "upstream_commit": results.upstream_commit,
        }
    )


def _build_session_artifact_index(
    request: Mapping[str, Any],
    child_result: Mapping[str, Any],
    request_artifact: Mapping[str, Any],
    child_result_artifact: Mapping[str, Any],
) -> Mapping[str, Any]:
    artifacts: list[Mapping[str, Any]] = []

    def add(
        kind: str,
        artifact: Mapping[str, Any],
        project_path: str | None = None,
    ) -> None:
        item: dict[str, Any] = {
            "bytes": artifact["bytes"],
            "kind": kind,
            "path": artifact["path"],
            "sha256": artifact["sha256"],
        }
        if project_path is not None:
            item["project_path"] = project_path
        artifacts.append(item)

    add("request", request_artifact)
    add("child_result", child_result_artifact)
    for project in request["projects"]:
        add("generated_build_props", project["build_props"], project["path"])
        add(
            "parent_evaluation_build_props",
            project["planning_build_props"],
            project["path"],
        )
    for project in child_result["projects"]:
        project_path = project["path"]
        add(
            "child_evaluation_build_props",
            project["evaluation_build_props"],
            project_path,
        )
        for key in (
            "restore_stderr",
            "restore_stdout",
            "stderr",
            "stdout",
            "test_dll",
            "trx",
        ):
            add(key, project[key], project_path)
        for key in ("implementation_dlls", "records"):
            singular = "implementation_dll" if key == "implementation_dlls" else "record"
            for artifact in project[key]:
                add(singular, artifact, project_path)
    for project in child_result["projects"]:
        add(
            "parent_validation_build_props",
            project["parent_validation_build_props"],
            project["path"],
        )
    artifacts.sort(key=lambda item: (item["path"], item["kind"]))
    paths = [item["path"] for item in artifacts]
    if paths != sorted(set(paths)):
        raise TrustedCollectorError("trusted session artifact index contains duplicate paths")
    project_count = len(request["projects"])
    assertion_count = len(child_result["assertions"])
    artifact_count = len(artifacts)
    if (
        project_count <= 0
        or assertion_count <= 0
        or artifact_count <= 0
        or request["project_count"] != project_count
        or child_result["project_count"] != project_count
        or request["assertion_count"] != assertion_count
        or child_result["assertion_count"] != assertion_count
        or child_result["artifact_count"] != artifact_count
    ):
        raise TrustedCollectorError("trusted session artifact index counts are inconsistent")
    return {
        "artifact_count": artifact_count,
        "artifacts": artifacts,
        "assertion_count": assertion_count,
        "child_result_sha256": child_result_artifact["sha256"],
        "dotnet_executable_sha256": request["dotnet"]["sha256"],
        "git_executable_sha256": request["git"]["sha256"],
        "project_count": project_count,
        "repository_head": request["repository_head"],
        "request_sha256": request_artifact["sha256"],
        "schema": ARTIFACT_INDEX_SCHEMA,
        "session_id": request["session_id"],
        "source_tree_sha256": request["source"]["sha256"],
        "target_framework": request["target_framework"],
        "toolchain_manifest_sha256": request["dotnet"]["sdk_manifest"]["sha256"],
    }


def _launch_isolated_child(
    repository_root: Path,
    envelope: Mapping[str, Any],
    *,
    timeout_seconds: int,
) -> Mapping[str, Any]:
    collector = repository_root.joinpath(*_COLLECTOR_PATH.split("/"))
    environment = _sanitized_environment()
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    try:
        completed = subprocess.run(
            [sys.executable, "-I", str(collector), "--child"],
            cwd=repository_root,
            env=environment,
            input=_canonical_json_bytes(envelope),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=timeout_seconds,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exception:
        raise TrustedCollectorError(
            f"cannot run trusted evidence child: {exception}"
        ) from exception
    if len(completed.stdout) > _MAX_CHILD_OUTPUT_BYTES or len(completed.stderr) > _MAX_CHILD_OUTPUT_BYTES:
        raise TrustedCollectorError("trusted evidence child output exceeded the safety limit")
    if completed.returncode != 0:
        detail = completed.stderr.decode("utf-8", errors="replace").strip()
        raise TrustedCollectorError(
            f"trusted evidence child failed: {detail or completed.returncode}"
        )
    try:
        value = _json_loads(completed.stdout.decode("utf-8"))
    except (UnicodeError, json.JSONDecodeError) as exception:
        raise TrustedCollectorError("trusted evidence child returned invalid JSON") from exception
    return _mapping(value, "trusted evidence child result")


def _child_entrypoint() -> int:
    try:
        raw = sys.stdin.buffer.read(_MAX_CHILD_OUTPUT_BYTES + 1)
        if len(raw) > _MAX_CHILD_OUTPUT_BYTES:
            raise TrustedCollectorError("trusted evidence request exceeded the safety limit")
        envelope = _mapping(
            _json_loads(raw.decode("utf-8")),
            "trusted evidence request envelope",
        )
        _exact_keys(envelope, {"request", "request_hmac", "secret"}, "request envelope")
        try:
            secret = base64.b64decode(_text(envelope["secret"], "request secret"), validate=True)
        except (ValueError, TypeError) as exception:
            raise TrustedCollectorError("trusted evidence request secret is invalid") from exception
        if len(secret) != _CHILD_SECRET_BYTES:
            raise TrustedCollectorError("trusted evidence request secret has an invalid length")
        request = _mapping(envelope["request"], "trusted evidence request")
        request_bytes = _canonical_json_bytes(request)
        expected = _hmac_sha256(secret, request_bytes)
        if not hmac.compare_digest(
            _text(envelope["request_hmac"], "request HMAC"),
            expected,
        ):
            raise TrustedCollectorError("trusted evidence request HMAC is invalid")
        result = _run_child_request(request)
        signed = {
            "payload": result,
            "result_hmac": _hmac_sha256(secret, _canonical_json_bytes(result)),
        }
        sys.stdout.buffer.write(_canonical_json_bytes(signed))
        return 0
    except Exception as exception:  # The parent treats every child failure as fail-closed.
        print(f"trusted-evidence-child: {exception}", file=sys.stderr)
        return 2


def _run_child_request(
    request: Mapping[str, Any],
    *,
    run_command: Callable[..., subprocess.CompletedProcess[bytes]] = subprocess.run,
) -> Mapping[str, Any]:
    normalized = _validate_request(request)
    root = Path(normalized["repository_root"])
    source_root = Path(normalized["source"]["root"])
    git = Path(normalized["git"]["path"])
    before = _exact_repository_snapshot(root, git, normalized["git"]["sha256"])
    if before[0] != normalized["repository_head"]:
        raise TrustedCollectorError("trusted child repository HEAD is stale")
    expected_source_files = {
        item["path"]: item["sha256"] for item in normalized["source"]["files"]
    }
    if before[1] != expected_source_files:
        raise TrustedCollectorError("trusted child source descriptor is not the exact HEAD tree")
    _verify_materialized_source(source_root, normalized["source"])
    _verify_msbuild_xml_sources(source_root, normalized["source"])
    _verify_requested_inputs(source_root, normalized["inputs"])
    _verify_canonical_evidence_binding(source_root, normalized["evidence_binding"])
    dotnet = Path(normalized["dotnet"]["path"])
    if _sha256_file(dotnet) != normalized["dotnet"]["sha256"]:
        raise TrustedCollectorError("trusted child dotnet executable hash is stale")
    actual_sdk_root = _verify_dotnet_version(
        source_root,
        dotnet,
        normalized["dotnet"]["sdk_version"],
        run_command,
        Path(normalized["session_directory"]) / "e" / "d1",
    )
    if actual_sdk_root.as_posix() != normalized["dotnet"]["sdk_root"]:
        raise TrustedCollectorError("trusted child dotnet SDK root is stale")
    _verify_sdk_toolchain_manifest(normalized["dotnet"]["sdk_manifest"])

    project_results: list[Mapping[str, Any]] = []
    for project in normalized["projects"]:
        evaluation_props = _write_project_build_props(
            Path(normalized["session_directory"]),
            source_root,
            project,
            normalized["source"],
            stage="g1",
        )
        evaluation = _evaluate_project_graph(
            source_root,
            Path(normalized["session_directory"]),
            project["path"],
            project["slug"],
            evaluation_props,
            normalized["source"],
            normalized["target_framework"],
            dotnet,
            normalized["dotnet"]["sdk_manifest"],
            "g1",
            run_command=run_command,
        )
        if evaluation != project["evaluated_graph"]:
            raise TrustedCollectorError(
                "child evaluated MSBuild graph differs from the parent request"
            )
        executed = _run_test_project(
            normalized,
            project,
            run_command=run_command,
        )
        project_results.append(
            {
                **executed,
                "evaluated_graph": evaluation,
                "evaluation_build_props": evaluation_props,
            }
        )
    assertions = sorted(
        (
            assertion
            for project in project_results
            for assertion in project["assertions"]
        ),
        key=lambda item: item["assertion_id"],
    )
    if [item["assertion_id"] for item in assertions] != normalized["required_assertion_ids"]:
        raise TrustedCollectorError("trusted child did not collect the exact required assertions")
    artifact_count = 2 + sum(
        10 + len(project["implementation_dlls"]) + len(project["records"])
        for project in project_results
    )
    _verify_requested_inputs(source_root, normalized["inputs"])
    _verify_materialized_source(source_root, normalized["source"])
    _verify_sdk_toolchain_manifest(normalized["dotnet"]["sdk_manifest"])
    after = _exact_repository_snapshot(root, git, normalized["git"]["sha256"])
    if after != before:
        raise TrustedCollectorError("repository changed while the trusted child was running")
    return {
        "artifact_count": artifact_count,
        "assertion_count": len(assertions),
        "assertions": assertions,
        "git_executable_sha256": normalized["git"]["sha256"],
        "inputs": normalized["inputs"],
        "nonce": normalized["nonce"],
        "package_locks": normalized["package_locks"],
        "project_count": len(project_results),
        "projects": project_results,
        "repository_head": normalized["repository_head"],
        "request_sha256": _sha256_data(normalized),
        "schema": CHILD_RESULT_SCHEMA,
        "session_id": normalized["session_id"],
        "source_tree_sha256": normalized["source"]["sha256"],
        "target_framework": normalized["target_framework"],
        "toolchain_manifest_sha256": normalized["dotnet"]["sdk_manifest"]["sha256"],
    }


def _run_test_project(
    request: Mapping[str, Any],
    project: Mapping[str, Any],
    *,
    run_command: Callable[..., subprocess.CompletedProcess[bytes]],
) -> Mapping[str, Any]:
    root = Path(request["source"]["root"])
    session = Path(request["session_directory"])
    slug = _text(project["slug"], "test project slug")
    project_session = session / "p" / slug
    if project_session.exists():
        raise TrustedCollectorError(f"test project session already exists: {slug}")
    bin_root = project_session / "b"
    obj_root = project_session / "o"
    results_root = project_session / "t"
    records_root = project_session / "r"
    user_extensions_root = project_session / "u"
    packages_root = session / "n" / "t" / slug
    environment_root = project_session / "e"
    for path in (
        bin_root,
        obj_root,
        results_root,
        records_root,
        user_extensions_root,
        packages_root,
    ):
        path.mkdir(parents=True, exist_ok=True)
        _require_safe_ancestors(session, path)

    environment = _isolated_dotnet_environment(
        environment_root, Path(request["dotnet"]["path"])
    )
    _require_safe_ancestors(session, environment_root)
    environment.update(
        {
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_MULTILEVEL_LOOKUP": "0",
            "DOTNET_NOLOGO": "1",
            "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
            "GONIEGONIE_EVIDENCE_RECORDS_DIRECTORY": str(records_root),
            "GONIEGONIE_EVIDENCE_SESSION_NONCE": request["nonce"],
            "NUGET_PACKAGES": str(packages_root),
        }
    )
    restore_command = _dotnet_restore_command(request, project)
    try:
        restore_completed = run_command(
            restore_command,
            cwd=root,
            env=environment,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=1800,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exception:
        raise TrustedCollectorError(
            f"locked restore failed to run for {project['path']}: {exception}"
        ) from exception
    restore_stdout = bytes(restore_completed.stdout or b"")
    restore_stderr = bytes(restore_completed.stderr or b"")
    if (
        len(restore_stdout) > _MAX_CHILD_OUTPUT_BYTES
        or len(restore_stderr) > _MAX_CHILD_OUTPUT_BYTES
    ):
        raise TrustedCollectorError("locked restore output exceeded the safety limit")
    restore_stdout_path = project_session / "restore.stdout.bin"
    restore_stderr_path = project_session / "restore.stderr.bin"
    if restore_completed.returncode != 0:
        _write_captured_session_artifact(
            session,
            restore_stdout_path,
            restore_stdout,
            "locked restore stdout",
        )
        _write_captured_session_artifact(
            session,
            restore_stderr_path,
            restore_stderr,
            "locked restore stderr",
        )
        detail = restore_stderr.decode("utf-8", errors="replace").strip()
        raise TrustedCollectorError(
            "locked restore failed for "
            f"{project['path']}: {detail or restore_completed.returncode}"
        )

    command = _dotnet_test_command(request, project, bin_root, obj_root, results_root)
    try:
        completed = run_command(
            command,
            cwd=root,
            env=environment,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=1800,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exception:
        raise TrustedCollectorError(f"dotnet test failed to run for {project['path']}: {exception}") from exception
    try:
        restore_stdout_artifact = _write_captured_session_artifact(
            session,
            restore_stdout_path,
            restore_stdout,
            "locked restore stdout",
        )
        restore_stderr_artifact = _write_captured_session_artifact(
            session,
            restore_stderr_path,
            restore_stderr,
            "locked restore stderr",
        )
    except (OSError, TrustedCollectorError) as exception:
        raise TrustedCollectorError(
            f"restore provenance path was preempted for {project['path']}"
        ) from exception
    stdout = bytes(completed.stdout or b"")
    stderr = bytes(completed.stderr or b"")
    if len(stdout) > _MAX_CHILD_OUTPUT_BYTES or len(stderr) > _MAX_CHILD_OUTPUT_BYTES:
        raise TrustedCollectorError("dotnet test output exceeded the safety limit")
    stdout_path = project_session / "stdout.bin"
    stderr_path = project_session / "stderr.bin"
    try:
        stdout_artifact = _write_captured_session_artifact(
            session,
            stdout_path,
            stdout,
            "dotnet test stdout",
        )
        stderr_artifact = _write_captured_session_artifact(
            session,
            stderr_path,
            stderr,
            "dotnet test stderr",
        )
    except (OSError, TrustedCollectorError) as exception:
        raise TrustedCollectorError(
            f"test provenance path was preempted for {project['path']}"
        ) from exception

    trx_files = tuple(results_root.glob("*.trx"))
    if len(trx_files) != 1:
        raise TrustedCollectorError(
            f"dotnet test must emit exactly one TRX for {project['path']}"
        )
    parsed = _parse_project_artifacts(
        request,
        project,
        trx_files[0],
        records_root,
        bin_root,
        int(completed.returncode),
    )
    return {
        "arguments": command,
        "assertions": parsed["assertions"],
        "exit_code": int(completed.returncode),
        "implementation_dlls": parsed["implementation_dlls"],
        "path": project["path"],
        "records": parsed["records"],
        "restore_arguments": restore_command,
        "restore_exit_code": int(restore_completed.returncode),
        "restore_stderr": restore_stderr_artifact,
        "restore_stdout": restore_stdout_artifact,
        "stderr": stderr_artifact,
        "stdout": stdout_artifact,
        "test_dll": parsed["test_dll"],
        "trx": _session_artifact(session, trx_files[0], max_bytes=_MAX_TRX_BYTES),
    }


def _nearest_tracked_project_file(
    project_path: str,
    source_files: set[str],
    name: str,
) -> str | None:
    current = PurePosixPath(project_path).parent
    while True:
        candidate = name if current.as_posix() == "." else f"{current.as_posix()}/{name}"
        if candidate in source_files:
            return candidate
        if current.as_posix() == ".":
            return None
        current = current.parent


def _write_project_build_props(
    session: Path,
    source_root: Path,
    project: Mapping[str, Any],
    source_tree: Mapping[str, Any],
    *,
    stage: str = "c",
) -> Mapping[str, Any]:
    slug = _text(project["slug"], "test project slug")
    if re.fullmatch(r"[a-z0-9]+", stage) is None:
        raise TrustedCollectorError("trusted build-props stage is invalid")
    directory = session / stage / slug
    directory.mkdir(parents=True, exist_ok=False)
    _require_safe_ancestors(session, directory)
    path = directory / "d.props"
    source_files = {item["path"] for item in source_tree["files"]}
    xml_root = ElementTree.Element("Project")
    project_paths = sorted(
        path for path in source_files if path.casefold().endswith(".csproj")
    )
    for project_path in project_paths:
        tracked_props = _nearest_tracked_project_file(
            project_path,
            source_files,
            "Directory.Build.props",
        )
        if tracked_props is None:
            continue
        ElementTree.SubElement(
            xml_root,
            "Import",
            {
                "Condition": (
                    "'$(MSBuildProjectFullPath)' == "
                    f"'{source_root.joinpath(*PurePosixPath(project_path).parts)}'"
                ),
                "Project": str(
                    source_root.joinpath(*PurePosixPath(tracked_props).parts)
                ),
            },
        )
    properties = ElementTree.SubElement(xml_root, "PropertyGroup")
    output_base = session / ("p" if stage == "c" else stage) / slug
    bin_root = output_base / "b"
    obj_root = output_base / "o"
    ElementTree.SubElement(properties, "GonieGonieProjectKey").text = (
        '$([MSBuild]::StableStringHash($(MSBuildProjectFullPath)).ToString("X8"))'
    )
    ElementTree.SubElement(properties, "BaseOutputPath").text = (
        f"{bin_root}{os.sep}$(GonieGonieProjectKey){os.sep}"
    )
    ElementTree.SubElement(properties, "BaseIntermediateOutputPath").text = (
        f"{obj_root}{os.sep}$(GonieGonieProjectKey){os.sep}"
    )
    ElementTree.SubElement(properties, "MSBuildProjectExtensionsPath").text = (
        "$(BaseIntermediateOutputPath)"
    )
    value = ElementTree.tostring(
        xml_root,
        encoding="utf-8",
        xml_declaration=True,
        short_empty_elements=True,
    )
    _write_exclusive(path, value)
    return _session_artifact(session, path)


def _dotnet_restore_command(
    request: Mapping[str, Any],
    project: Mapping[str, Any],
) -> list[str]:
    source_root = Path(request["source"]["root"])
    source_paths = {item["path"] for item in request["source"]["files"]}
    session = Path(request["session_directory"])
    slug = _text(project["slug"], "test project slug")
    packages_root = session / "n" / "t" / slug
    user_extensions_root = session / "p" / slug / "u"
    build_props = _artifact_path(
        session,
        project["build_props"],
        "project build props",
    )
    properties = _msbuild_isolation_properties(
        source_root,
        source_paths,
        project["path"],
        build_props,
        None,
        Path(request["dotnet"]["sdk_root"]),
        packages_root,
        user_extensions_root,
    )
    return [
        request["dotnet"]["path"],
        "restore",
        "/noAutoResponse",
        str(source_root.joinpath(*project["path"].split("/"))),
        "--locked-mode",
        "--configfile",
        str(source_root / "NuGet.config"),
        "--packages",
        str(packages_root),
        "--disable-build-servers",
        *properties,
    ]


def _dotnet_test_command(
    request: Mapping[str, Any],
    project: Mapping[str, Any],
    bin_root: Path,
    obj_root: Path,
    results_root: Path,
) -> list[str]:
    del bin_root, obj_root
    source_root = Path(request["source"]["root"])
    source_paths = {item["path"] for item in request["source"]["files"]}
    directory_build_targets_relative = _nearest_tracked_project_file(
        project["path"], source_paths, "Directory.Build.targets"
    )
    directory_packages_props_relative = _nearest_tracked_project_file(
        project["path"], source_paths, "Directory.Packages.props"
    )
    directory_build_targets = (
        None
        if directory_build_targets_relative is None
        else str(
            source_root.joinpath(*PurePosixPath(directory_build_targets_relative).parts)
        )
    )
    directory_packages_props = (
        None
        if directory_packages_props_relative is None
        else str(
            source_root.joinpath(*PurePosixPath(directory_packages_props_relative).parts)
        )
    )
    nuget_config = source_root / "NuGet.config"
    build_props = _artifact_path(
        Path(request["session_directory"]),
        project["build_props"],
        "project build props",
    )
    command = [
        request["dotnet"]["path"],
        "test",
        "/noAutoResponse",
        str(source_root.joinpath(*project["path"].split("/"))),
        "--configuration",
        "Release",
        "--framework",
        request["target_framework"],
        "--no-restore",
        "--logger",
        f"trx;LogFileName={project['slug']}.trx",
        "--results-directory",
        str(results_root),
        "--disable-build-servers",
        "-p:ContinuousIntegrationBuild=true",
        "-p:Deterministic=true",
        f"-p:PathMap={request['source']['root']}=/_/",
        "-p:RestoreLockedMode=true",
        "-p:RestorePackagesWithLockFile=true",
        "-p:RestoreNoCache=true",
        "-p:RestoreDisableParallel=true",
        "-p:NuGetAudit=false",
        f"-p:RestoreConfigFile={nuget_config}",
        f"-p:RestorePackagesPath={Path(request['session_directory']) / 'n' / 't' / project['slug']}",
        f"-p:MSBuildSDKsPath={Path(request['dotnet']['sdk_root']) / 'Sdks'}",
        f"-p:MSBuildUserExtensionsPath={Path(request['session_directory']) / 'p' / project['slug'] / 'u'}",
        (
            "-p:CustomBeforeMicrosoftCommonTargets="
            f"{source_root / '.goniegonie-no-custom-before.targets'}"
        ),
        (
            "-p:CustomAfterMicrosoftCommonTargets="
            f"{source_root / '.goniegonie-no-custom-after.targets'}"
        ),
    ]
    command.extend(
        (
            "-p:ImportDirectoryBuildProps=true",
            f"-p:DirectoryBuildPropsPath={build_props}",
        )
    )
    if directory_build_targets is None:
        command.append("-p:ImportDirectoryBuildTargets=false")
    else:
        command.extend(
            (
                "-p:ImportDirectoryBuildTargets=true",
                f"-p:DirectoryBuildTargetsPath={directory_build_targets}",
            )
        )
    if directory_packages_props is None:
        command.append("-p:ImportDirectoryPackagesProps=false")
    else:
        command.extend(
            (
                "-p:ImportDirectoryPackagesProps=true",
                f"-p:DirectoryPackagesPropsPath={directory_packages_props}",
            )
        )
    return command


def _parse_project_artifacts(
    request: Mapping[str, Any],
    project: Mapping[str, Any],
    trx_path: Path,
    records_root: Path,
    bin_root: Path,
    exit_code: int,
) -> Mapping[str, Any]:
    session = Path(request["session_directory"])
    for path, context in (
        (bin_root, "fresh build output directory"),
        (records_root, "evidence record directory"),
        (trx_path.parent, "test results directory"),
    ):
        _require_safe_ancestors(session, path)
        _require_directory(path, context)
    _require_safe_ancestors(session, trx_path.parent)
    _require_regular_unlinked_file(trx_path, "TRX")
    if trx_path.stat().st_size > _MAX_TRX_BYTES:
        raise TrustedCollectorError("TRX exceeded the safety limit")
    try:
        raw_trx = trx_path.read_bytes()
        if re.search(br"<!\s*(?:DOCTYPE|ENTITY)\b", raw_trx, flags=re.IGNORECASE):
            raise TrustedCollectorError("TRX must not contain DOCTYPE or ENTITY declarations")
        root_element = ElementTree.fromstring(raw_trx)
    except (OSError, ElementTree.ParseError) as exception:
        raise TrustedCollectorError(f"cannot parse TRX '{trx_path}': {exception}") from exception
    if root_element.tag != f"{{{_TRX_NAMESPACE}}}TestRun":
        raise TrustedCollectorError("TRX uses an unexpected root or namespace")
    namespace = {"t": _TRX_NAMESPACE}
    definitions: dict[str, Mapping[str, str]] = {}
    for unit_test in root_element.findall("./t:TestDefinitions/t:UnitTest", namespace):
        identifier = unit_test.get("id")
        execution = unit_test.find("./t:Execution", namespace)
        method = unit_test.find("./t:TestMethod", namespace)
        if not identifier or execution is None or method is None:
            raise TrustedCollectorError("TRX contains an incomplete UnitTest definition")
        execution_id = execution.get("id")
        required_attributes = {
            "class_name": method.get("className"),
            "code_base": method.get("codeBase"),
            "method_name": method.get("name"),
        }
        if not execution_id or any(not value for value in required_attributes.values()):
            raise TrustedCollectorError("TRX test definition is missing exact binding metadata")
        if identifier in definitions:
            raise TrustedCollectorError("TRX contains a duplicate test definition id")
        definitions[identifier] = {
            "execution_id": execution_id,
            **{key: str(value) for key, value in required_attributes.items()},
        }
    results: dict[str, Mapping[str, str]] = {}
    for result in root_element.findall("./t:Results/t:UnitTestResult", namespace):
        test_id = result.get("testId")
        execution_id = result.get("executionId")
        test_name = result.get("testName")
        outcome = result.get("outcome")
        if not all((test_id, execution_id, test_name, outcome)):
            raise TrustedCollectorError("TRX result is missing exact execution metadata")
        key = f"{test_id}\0{execution_id}"
        if key in results:
            raise TrustedCollectorError("TRX contains a duplicate test execution")
        results[key] = {
            "execution_id": str(execution_id),
            "outcome": str(outcome),
            "test_id": str(test_id),
            "test_name": str(test_name),
        }

    test_dll = _find_exact_assembly(bin_root, project["assembly_name"])
    test_dll_resolved = test_dll.resolve(strict=True)
    records = _load_execution_records(records_root, request["nonce"])
    assertions: list[Mapping[str, Any]] = []
    used_records: set[tuple[str, str]] = set()
    for assertion in project["assertions"]:
        owner, method_name = assertion["test_symbol"].rsplit(".", 1)
        matched: list[tuple[Mapping[str, str], Mapping[str, str]]] = []
        for test_id, definition in definitions.items():
            if definition["class_name"] != owner or definition["method_name"] != method_name:
                continue
            execution_key = f"{test_id}\0{definition['execution_id']}"
            actual_result = results.get(execution_key)
            if actual_result is None:
                raise TrustedCollectorError(
                    f"TRX has no result for {assertion['test_symbol']}"
                )
            code_base = _resolve_code_base(definition["code_base"], trx_path, bin_root)
            if code_base != test_dll_resolved:
                raise TrustedCollectorError(
                    f"TRX codeBase for {assertion['test_symbol']} is not the fresh test DLL"
                )
            matched.append((definition, actual_result))
        if not matched:
            raise TrustedCollectorError(
                f"TRX does not contain exact test symbol {assertion['test_symbol']}"
            )
        test_names = [item[1]["test_name"] for item in matched]
        if len(test_names) != len(set(test_names)):
            raise TrustedCollectorError(
                f"TRX theory cases for {assertion['test_symbol']} are ambiguous"
            )
        case_payloads: list[Mapping[str, Any]] = []
        case_outcomes: list[str] = []
        for _, actual_result in sorted(matched, key=lambda item: item[1]["test_name"]):
            record_key = (assertion["id"], actual_result["test_name"])
            record = records.get(record_key)
            if record is None:
                raise TrustedCollectorError(
                    f"missing deterministic record for {assertion['id']} case {actual_result['test_name']}"
                )
            used_records.add(record_key)
            if record["structural_only"]:
                raise TrustedCollectorError(
                    f"assertion {assertion['id']} emitted structural-only evidence"
                )
            if record["exercised_load"] != assertion["exercised_load"]:
                raise TrustedCollectorError(
                    f"assertion {assertion['id']} emitted the wrong exercised load"
                )
            case_payloads.append(
                {"output": record["output"], "test_case": actual_result["test_name"]}
            )
            case_outcomes.append(actual_result["outcome"])
        output_hash = _sha256_data({"cases": case_payloads})
        skipped = any(item in {"NotExecuted", "NotRunnable", "Ignored"} for item in case_outcomes)
        passed = exit_code == 0 and all(item == "Passed" for item in case_outcomes)
        assertions.append(
            {
                "assertion_id": assertion["id"],
                "exercised_load": assertion["exercised_load"],
                "outcome": "passed" if passed else ("skipped" if skipped else "failed"),
                "output_sha256": output_hash,
                "skipped": skipped,
                "structural_only": False,
                "test_path": assertion["test_path"],
                "test_source_sha256": assertion["test_source_sha256"],
                "test_symbol": assertion["test_symbol"],
            }
        )
    if set(records) != used_records:
        raise TrustedCollectorError("record directory contains undeclared or unexecuted evidence")
    implementation_dlls = [
        _session_artifact(
            session,
            _find_exact_assembly(
                bin_root,
                assembly_name,
                allow_identical_copies=True,
            ),
        )
        for assembly_name in project["implementation_assemblies"]
    ]
    return {
        "assertions": sorted(assertions, key=lambda item: item["assertion_id"]),
        "implementation_dlls": implementation_dlls,
        "records": [
            _session_artifact(session, records[key]["path"], max_bytes=_MAX_RECORD_BYTES)
            for key in sorted(records)
        ],
        "test_dll": _session_artifact(session, test_dll),
    }


def _load_execution_records(
    records_root: Path,
    nonce: str,
) -> dict[tuple[str, str], Mapping[str, Any]]:
    _require_directory(records_root, "evidence record directory")
    result: dict[tuple[str, str], Mapping[str, Any]] = {}
    for path in sorted(records_root.iterdir(), key=lambda item: item.name):
        _require_safe_ancestors(records_root, path.parent)
        if path.suffix.lower() != ".json" or not path.is_file():
            raise TrustedCollectorError("record directory must contain only exact JSON files")
        _require_regular_unlinked_file(path, "evidence record")
        if path.stat().st_size > _MAX_RECORD_BYTES:
            raise TrustedCollectorError("evidence record exceeded the safety limit")
        try:
            value = _json_loads(path.read_text(encoding="utf-8"))
        except (OSError, UnicodeError, json.JSONDecodeError) as exception:
            raise TrustedCollectorError(f"invalid evidence record '{path.name}'") from exception
        item = _mapping(value, f"evidence record {path.name}")
        _exact_keys(
            item,
            {
                "assertion_id",
                "exercised_load",
                "output",
                "schema",
                "session_nonce",
                "structural_only",
                "test_case",
            },
            f"evidence record {path.name}",
        )
        if item["schema"] != RECORD_SCHEMA or item["session_nonce"] != nonce:
            raise TrustedCollectorError("evidence record schema or nonce is invalid")
        identifier = _identifier(item["assertion_id"], "record assertion id")
        test_case = _text(item["test_case"], "record test case")
        exercised_load = _text(item["exercised_load"], "record exercised load")
        if exercised_load not in _LOAD_CASES:
            raise TrustedCollectorError("record exercised load is invalid")
        if not isinstance(item["structural_only"], bool):
            raise TrustedCollectorError("record structural_only must be boolean")
        _canonical_json_bytes(item["output"])
        key = (identifier, test_case)
        if key in result:
            raise TrustedCollectorError("duplicate assertion/test-case evidence record")
        result[key] = {
            "exercised_load": exercised_load,
            "output": item["output"],
            "path": path,
            "structural_only": item["structural_only"],
        }
    return result


def _validate_child_result_artifacts(
    request: Mapping[str, Any],
    signed: Mapping[str, Any],
    secret: bytes,
) -> Mapping[str, Any]:
    _exact_keys(signed, {"payload", "result_hmac"}, "signed child result")
    payload = _mapping(signed["payload"], "child result payload")
    expected_hmac = _hmac_sha256(secret, _canonical_json_bytes(payload))
    if not hmac.compare_digest(
        _text(signed["result_hmac"], "child result HMAC"),
        expected_hmac,
    ):
        raise TrustedCollectorError("trusted evidence child result HMAC is invalid")
    normalized_request = _validate_request(request)
    _exact_keys(
        payload,
        {
            "artifact_count",
            "assertion_count",
            "assertions",
            "git_executable_sha256",
            "inputs",
            "nonce",
            "package_locks",
            "project_count",
            "projects",
            "repository_head",
            "request_sha256",
            "schema",
            "session_id",
            "source_tree_sha256",
            "target_framework",
            "toolchain_manifest_sha256",
        },
        "child result payload",
    )
    if payload["schema"] != CHILD_RESULT_SCHEMA:
        raise TrustedCollectorError("trusted evidence child result schema is invalid")
    for key in ("project_count", "assertion_count", "artifact_count"):
        count = payload[key]
        if not isinstance(count, int) or isinstance(count, bool) or count <= 0:
            raise TrustedCollectorError(f"trusted child result {key} is invalid")
    exact_bindings = {
        "assertion_count": normalized_request["assertion_count"],
        "inputs": normalized_request["inputs"],
        "git_executable_sha256": normalized_request["git"]["sha256"],
        "nonce": normalized_request["nonce"],
        "package_locks": normalized_request["package_locks"],
        "project_count": normalized_request["project_count"],
        "repository_head": normalized_request["repository_head"],
        "request_sha256": _sha256_data(normalized_request),
        "session_id": normalized_request["session_id"],
        "source_tree_sha256": normalized_request["source"]["sha256"],
        "target_framework": normalized_request["target_framework"],
        "toolchain_manifest_sha256": normalized_request["dotnet"]["sdk_manifest"]["sha256"],
    }
    for key, expected in exact_bindings.items():
        if payload[key] != expected:
            raise TrustedCollectorError(f"trusted child result has a stale {key} binding")

    root = Path(normalized_request["repository_root"])
    session = Path(normalized_request["session_directory"])
    source_root = Path(normalized_request["source"]["root"])
    git = Path(normalized_request["git"]["path"])
    snapshot = _exact_repository_snapshot(
        root,
        git,
        normalized_request["git"]["sha256"],
    )
    expected_source_files = {
        item["path"]: item["sha256"] for item in normalized_request["source"]["files"]
    }
    if snapshot[0] != normalized_request["repository_head"] or snapshot[1] != expected_source_files:
        raise TrustedCollectorError("parent source descriptor is not the exact repository HEAD")
    _verify_materialized_source(source_root, normalized_request["source"])
    _verify_msbuild_xml_sources(source_root, normalized_request["source"])
    _verify_sdk_toolchain_manifest(normalized_request["dotnet"]["sdk_manifest"])
    _verify_requested_inputs(source_root, normalized_request["inputs"])
    _verify_canonical_evidence_binding(
        source_root,
        normalized_request["evidence_binding"],
    )
    dotnet = Path(normalized_request["dotnet"]["path"])
    if _sha256_file(dotnet) != normalized_request["dotnet"]["sha256"]:
        raise TrustedCollectorError("trusted child result dotnet executable hash is stale")
    actual_sdk_root = _verify_dotnet_version(
        source_root,
        dotnet,
        normalized_request["dotnet"]["sdk_version"],
        subprocess.run,
        session / "e" / "d2",
    )
    if actual_sdk_root.as_posix() != normalized_request["dotnet"]["sdk_root"]:
        raise TrustedCollectorError("trusted child result SDK root is stale")
    _verify_sdk_toolchain_manifest(normalized_request["dotnet"]["sdk_manifest"])
    projects = _sequence(payload["projects"], "child result projects")
    if (
        len(projects) != payload["project_count"]
        or len(projects) != len(normalized_request["projects"])
    ):
        raise TrustedCollectorError("trusted child returned the wrong project count")
    recomputed_assertions: list[Mapping[str, Any]] = []
    validated_projects: list[Mapping[str, Any]] = []
    recomputed_artifact_count = 2
    for expected_project, actual_value in zip(normalized_request["projects"], projects):
        actual = _mapping(actual_value, "child result project")
        _exact_keys(
            actual,
            {
                "arguments",
                "assertions",
                "evaluated_graph",
                "evaluation_build_props",
                "exit_code",
                "implementation_dlls",
                "path",
                "records",
                "restore_arguments",
                "restore_exit_code",
                "restore_stderr",
                "restore_stdout",
                "stderr",
                "stdout",
                "test_dll",
                "trx",
            },
            "child result project",
        )
        if actual["path"] != expected_project["path"]:
            raise TrustedCollectorError("trusted child project order/path is invalid")
        _validate_session_artifact(
            session,
            actual["evaluation_build_props"],
            "child evaluation build props",
        )
        expected_child_props = f"g1/{expected_project['slug']}/d.props"
        if actual["evaluation_build_props"]["path"] != expected_child_props:
            raise TrustedCollectorError("child evaluation build props path is invalid")
        normalized_child_graph = _normalize_evaluated_graph(actual["evaluated_graph"])
        if normalized_child_graph != expected_project["evaluated_graph"]:
            raise TrustedCollectorError("child evaluated MSBuild graph binding is stale")
        validation_props = _write_project_build_props(
            session,
            source_root,
            expected_project,
            normalized_request["source"],
            stage="g2",
        )
        parent_graph = _evaluate_project_graph(
            source_root,
            session,
            expected_project["path"],
            expected_project["slug"],
            validation_props,
            normalized_request["source"],
            normalized_request["target_framework"],
            dotnet,
            normalized_request["dotnet"]["sdk_manifest"],
            "g2",
            run_command=subprocess.run,
        )
        if parent_graph != expected_project["evaluated_graph"]:
            raise TrustedCollectorError(
                "parent re-evaluated MSBuild graph differs from the signed request"
            )
        validated_project = dict(actual)
        validated_project["parent_validation_build_props"] = validation_props
        validated_projects.append(validated_project)
        slug = expected_project["slug"]
        base = session / "p" / slug
        expected_command = _dotnet_test_command(
            normalized_request,
            expected_project,
            base / "b",
            base / "o",
            base / "t",
        )
        if actual["arguments"] != expected_command:
            raise TrustedCollectorError("trusted child dotnet command was not exact")
        expected_restore_command = _dotnet_restore_command(
            normalized_request,
            expected_project,
        )
        if actual["restore_arguments"] != expected_restore_command:
            raise TrustedCollectorError(
                "trusted child locked restore command was not exact"
            )
        restore_exit_code = actual["restore_exit_code"]
        if (
            not isinstance(restore_exit_code, int)
            or isinstance(restore_exit_code, bool)
            or restore_exit_code != 0
        ):
            raise TrustedCollectorError(
                "trusted child locked restore exit code is invalid"
            )
        exit_code = actual["exit_code"]
        if not isinstance(exit_code, int) or isinstance(exit_code, bool):
            raise TrustedCollectorError("trusted child exit code is invalid")
        for key in (
            "restore_stdout",
            "restore_stderr",
            "stdout",
            "stderr",
            "trx",
            "test_dll",
        ):
            _validate_session_artifact(session, actual[key], key)
        for key in ("implementation_dlls", "records"):
            for artifact in _sequence(actual[key], f"child result {key}"):
                _validate_session_artifact(session, artifact, key)
        recomputed_artifact_count += (
            10 + len(actual["implementation_dlls"]) + len(actual["records"])
        )
        recomputed = _parse_project_artifacts(
            normalized_request,
            expected_project,
            _artifact_path(session, actual["trx"], "TRX"),
            base / "r",
            base / "b",
            exit_code,
        )
        for key in ("assertions", "implementation_dlls", "records", "test_dll"):
            if actual[key] != recomputed[key]:
                raise TrustedCollectorError(
                    f"trusted child {key} does not match independently parsed artifacts"
                )
        recomputed_assertions.extend(recomputed["assertions"])
    recomputed_assertions.sort(key=lambda item: item["assertion_id"])
    if (
        payload["assertion_count"] != len(recomputed_assertions)
        or payload["assertions"] != recomputed_assertions
    ):
        raise TrustedCollectorError("trusted child aggregate assertions are inconsistent")
    if payload["artifact_count"] != recomputed_artifact_count:
        raise TrustedCollectorError("trusted child artifact_count is inconsistent")
    if [item["assertion_id"] for item in recomputed_assertions] != normalized_request["required_assertion_ids"]:
        raise TrustedCollectorError("trusted child aggregate assertion ids are incomplete")
    _verify_materialized_source(source_root, normalized_request["source"])
    final_snapshot = _exact_repository_snapshot(
        root,
        git,
        normalized_request["git"]["sha256"],
    )
    if final_snapshot != snapshot:
        raise TrustedCollectorError("repository changed during parent child-result validation")
    return {**payload, "projects": validated_projects}


def _validate_request(request: Mapping[str, Any]) -> Mapping[str, Any]:
    _exact_keys(
        request,
        {
            "assertion_count",
            "dotnet",
            "evidence_binding",
            "git",
            "inputs",
            "nonce",
            "package_locks",
            "project_count",
            "projects",
            "repository_head",
            "repository_root",
            "required_assertion_ids",
            "schema",
            "session_directory",
            "session_id",
            "source",
            "target_framework",
        },
        "trusted evidence request",
    )
    if request["schema"] != REQUEST_SCHEMA:
        raise TrustedCollectorError("trusted evidence request schema is invalid")
    root = Path(_text(request["repository_root"], "request repository root"))
    session = Path(_text(request["session_directory"], "request session directory"))
    if not root.is_absolute() or not session.is_absolute():
        raise TrustedCollectorError("trusted request paths must be absolute")
    root = root.resolve(strict=True)
    session = session.resolve(strict=True)
    expected_parent = root / "temp" / "u"
    if session.parent != expected_parent:
        raise TrustedCollectorError("trusted child session escaped the canonical session root")
    session_id = _text(request["session_id"], "request session id")
    if re.fullmatch(r"[0-9a-f]{32}", session_id) is None or session.name != session_id:
        raise TrustedCollectorError("trusted request session id is invalid")
    nonce = _text(request["nonce"], "request nonce")
    if re.fullmatch(r"[0-9a-f]{64}", nonce) is None:
        raise TrustedCollectorError("trusted request nonce is invalid")
    head = _text(request["repository_head"], "request repository HEAD")
    if _COMMIT.fullmatch(head) is None:
        raise TrustedCollectorError("trusted request repository HEAD is invalid")
    target_framework = _text(request["target_framework"], "request target framework")
    if re.fullmatch(r"net[0-9]+\.[0-9]+-windows", target_framework) is None:
        raise TrustedCollectorError("trusted request target framework is invalid")
    dotnet = _mapping(request["dotnet"], "request dotnet")
    _exact_keys(
        dotnet,
        {"path", "sdk_manifest", "sdk_root", "sdk_version", "sha256"},
        "request dotnet",
    )
    dotnet_path = Path(_text(dotnet["path"], "request dotnet path"))
    if not dotnet_path.is_absolute() or not dotnet_path.is_file():
        raise TrustedCollectorError("trusted request dotnet path is not an exact executable")
    sdk_version = _text(dotnet["sdk_version"], "request SDK version")
    if _SDK_VERSION.fullmatch(sdk_version) is None:
        raise TrustedCollectorError("trusted request SDK version is invalid")
    git = _mapping(request["git"], "request git")
    _exact_keys(git, {"path", "sha256"}, "request git")
    git_path = Path(_text(git["path"], "request git path"))
    if not git_path.is_absolute() or not git_path.is_file():
        raise TrustedCollectorError("trusted request git path is not an exact executable")
    git_path = git_path.resolve(strict=True)
    git_hash = _hash(git["sha256"], "request git executable hash")
    _require_regular_file(git_path, "trusted request git executable")
    if _sha256_file(git_path) != git_hash:
        raise TrustedCollectorError("trusted request git executable hash is stale")
    inputs = _normalize_inputs(request["inputs"])
    evidence_binding = _mapping(request["evidence_binding"], "request evidence binding")
    _exact_keys(
        evidence_binding,
        {
            "collector_path",
            "collector_source_sha256",
            "collector_symbol",
            "inventory_sha256",
            "matrix_sha256",
            "symbol_evidence_sha256",
            "upstream_commit",
        },
        "request evidence binding",
    )
    normalized_binding = {
        "collector_path": _relative_path(
            evidence_binding["collector_path"], "request collector path"
        ),
        "collector_source_sha256": _hash(
            evidence_binding["collector_source_sha256"], "request collector source hash"
        ),
        "collector_symbol": _symbol(
            evidence_binding["collector_symbol"], "request collector symbol"
        ),
        "inventory_sha256": _hash(
            evidence_binding["inventory_sha256"], "request inventory hash"
        ),
        "matrix_sha256": _hash(
            evidence_binding["matrix_sha256"], "request matrix hash"
        ),
        "symbol_evidence_sha256": _hash(
            evidence_binding["symbol_evidence_sha256"], "request symbol evidence hash"
        ),
        "upstream_commit": _text(
            evidence_binding["upstream_commit"], "request upstream commit"
        ),
    }
    if _COMMIT.fullmatch(normalized_binding["upstream_commit"]) is None:
        raise TrustedCollectorError("request upstream commit is invalid")
    input_by_path = {item["path"]: item["sha256"] for item in inputs}
    if (
        normalized_binding["collector_path"] != _COLLECTOR_PATH
        or normalized_binding["collector_symbol"] != _COLLECTOR_SYMBOL
        or input_by_path.get(_COLLECTOR_PATH)
        != normalized_binding["collector_source_sha256"]
    ):
        raise TrustedCollectorError("request collector binding is not canonical")
    package_locks = _normalize_inputs(request["package_locks"])
    expected_locks = [
        item for item in inputs if PurePosixPath(item["path"]).name == "packages.lock.json"
    ]
    if package_locks != expected_locks:
        raise TrustedCollectorError("request package-lock closure is not exact")
    sdk_root = Path(_text(dotnet["sdk_root"], "request SDK root"))
    if not sdk_root.is_absolute() or not sdk_root.is_dir():
        raise TrustedCollectorError("trusted request SDK root is invalid")
    sdk_root = sdk_root.resolve(strict=True)
    sdk_manifest = _normalize_sdk_manifest(dotnet["sdk_manifest"], sdk_root)
    projects = _normalize_projects(request["projects"])
    for project in projects:
        expected_build_props = (
            f"c/{project['slug']}/d.props"
        )
        if project["build_props"]["path"] != expected_build_props:
            raise TrustedCollectorError(
                "request project build props escaped its exact session location"
            )
        _validate_session_artifact(
            session,
            project["build_props"],
            "request project build props",
        )
        expected_planning_props = f"g0/{project['slug']}/d.props"
        if project["planning_build_props"]["path"] != expected_planning_props:
            raise TrustedCollectorError(
                "request planning build props escaped its exact session location"
            )
        _validate_session_artifact(
            session,
            project["planning_build_props"],
            "request planning build props",
        )
        if project["evaluated_graph"]["target_framework"] != target_framework:
            raise TrustedCollectorError(
                "request project evaluated graph targets a different framework"
            )
    source = _normalize_source_tree(request["source"], session)
    required = _normalize_identifiers(
        request["required_assertion_ids"],
        "request required assertion ids",
    )
    planned = sorted(
        assertion["id"]
        for project in projects
        for assertion in project["assertions"]
    )
    if planned != required:
        raise TrustedCollectorError("request project plan does not exactly cover required assertions")
    project_count = request["project_count"]
    assertion_count = request["assertion_count"]
    if (
        not isinstance(project_count, int)
        or isinstance(project_count, bool)
        or project_count < 0
        or project_count != len(projects)
    ):
        raise TrustedCollectorError("request project_count is invalid")
    if (
        not isinstance(assertion_count, int)
        or isinstance(assertion_count, bool)
        or assertion_count < 0
        or assertion_count != len(required)
    ):
        raise TrustedCollectorError("request assertion_count is invalid")
    normalized = {
        "assertion_count": assertion_count,
        "dotnet": {
            "path": dotnet_path.as_posix(),
            "sdk_manifest": sdk_manifest,
            "sdk_root": sdk_root.as_posix(),
            "sdk_version": sdk_version,
            "sha256": _hash(dotnet["sha256"], "request dotnet executable hash"),
        },
        "evidence_binding": normalized_binding,
        "git": {
            "path": git_path.as_posix(),
            "sha256": git_hash,
        },
        "inputs": inputs,
        "nonce": nonce,
        "package_locks": package_locks,
        "project_count": project_count,
        "projects": projects,
        "repository_head": head,
        "repository_root": root.as_posix(),
        "required_assertion_ids": required,
        "schema": REQUEST_SCHEMA,
        "session_directory": session.as_posix(),
        "session_id": session_id,
        "source": source,
        "target_framework": target_framework,
    }
    for project in projects:
        base = session / "p" / project["slug"]
        if project["arguments"] != _dotnet_test_command(
            normalized,
            project,
            base / "b",
            base / "o",
            base / "t",
        ):
            raise TrustedCollectorError(
                "request project dotnet arguments are not independently reproducible"
            )
        if project["restore_arguments"] != _dotnet_restore_command(
            normalized,
            project,
        ):
            raise TrustedCollectorError(
                "request project restore arguments are not independently reproducible"
            )
    return normalized


def _normalize_inputs(value: Any) -> list[Mapping[str, str]]:
    result: list[Mapping[str, str]] = []
    for raw in _sequence(value, "request inputs"):
        item = _mapping(raw, "request input")
        _exact_keys(item, {"path", "sha256"}, "request input")
        result.append(
            {
                "path": _relative_path(item["path"], "request input path"),
                "sha256": _hash(item["sha256"], "request input hash"),
            }
        )
    paths = [item["path"] for item in result]
    if paths != sorted(set(paths)):
        raise TrustedCollectorError("request inputs must be unique and sorted")
    return result


def _normalize_source_tree(value: Any, session: Path) -> Mapping[str, Any]:
    item = _mapping(value, "request source tree")
    _exact_keys(item, {"file_count", "files", "root", "sha256"}, "request source tree")
    source_root = Path(_text(item["root"], "request source root"))
    expected_root = session / "s"
    if not source_root.is_absolute() or source_root.resolve(strict=True) != expected_root:
        raise TrustedCollectorError("trusted request source root is not session/s")
    files = _normalize_inputs(item["files"])
    if any(".git" in {part.casefold() for part in PurePosixPath(entry["path"]).parts} for entry in files):
        raise TrustedCollectorError("materialized source must not contain .git")
    file_count = item["file_count"]
    if not isinstance(file_count, int) or isinstance(file_count, bool) or file_count != len(files):
        raise TrustedCollectorError("request source-tree file count is invalid")
    expected_hash = _sha256_data({"files": files})
    if _hash(item["sha256"], "request source-tree hash") != expected_hash:
        raise TrustedCollectorError("request source-tree aggregate hash is invalid")
    return {
        "file_count": file_count,
        "files": files,
        "root": source_root.as_posix(),
        "sha256": expected_hash,
    }


def _normalize_projects(value: Any) -> list[Mapping[str, Any]]:
    result: list[Mapping[str, Any]] = []
    for raw in _sequence(value, "request projects"):
        item = _mapping(raw, "request project")
        _exact_keys(
            item,
            {
                "arguments",
                "assembly_name",
                "assertions",
                "build_props",
                "evaluated_graph",
                "implementation_assemblies",
                "path",
                "planning_build_props",
                "restore_arguments",
                "slug",
            },
            "request project",
        )
        assertions: list[Mapping[str, Any]] = []
        for raw_assertion in _sequence(item["assertions"], "request project assertions"):
            assertion = _mapping(raw_assertion, "request assertion")
            _exact_keys(
                assertion,
                {
                    "exercised_load",
                    "id",
                    "test_path",
                    "test_source_sha256",
                    "test_symbol",
                },
                "request assertion",
            )
            load = _text(assertion["exercised_load"], "request assertion load")
            if load not in _LOAD_CASES:
                raise TrustedCollectorError("request assertion load is invalid")
            assertions.append(
                {
                    "exercised_load": load,
                    "id": _identifier(assertion["id"], "request assertion id"),
                    "test_path": _relative_path(assertion["test_path"], "request test path"),
                    "test_source_sha256": _hash(
                        assertion["test_source_sha256"], "request test source hash"
                    ),
                    "test_symbol": _symbol(assertion["test_symbol"], "request test symbol"),
                }
            )
        assertion_ids = [entry["id"] for entry in assertions]
        if assertion_ids != sorted(set(assertion_ids)):
            raise TrustedCollectorError("request project assertions must be unique and sorted")
        assemblies = _normalize_text_sequence(
            item["implementation_assemblies"],
            "implementation assemblies",
            pattern=r"[A-Za-z_][A-Za-z0-9_.-]*",
        )
        build_props = _mapping(item["build_props"], "request project build props")
        _exact_keys(
            build_props,
            {"bytes", "path", "sha256"},
            "request project build props",
        )
        planning_build_props = _mapping(
            item["planning_build_props"], "request planning build props"
        )
        _exact_keys(
            planning_build_props,
            {"bytes", "path", "sha256"},
            "request planning build props",
        )
        result.append(
            {
                "arguments": [
                    _text(argument, "request project dotnet argument")
                    for argument in _sequence(
                        item["arguments"], "request project dotnet arguments"
                    )
                ],
                "assembly_name": _pattern_text(
                    item["assembly_name"], r"[A-Za-z_][A-Za-z0-9_.-]*", "test assembly name"
                ),
                "assertions": assertions,
                "build_props": {
                    "bytes": build_props["bytes"],
                    "path": _relative_path(
                        build_props["path"],
                        "request project build props path",
                    ),
                    "sha256": _hash(
                        build_props["sha256"],
                        "request project build props hash",
                    ),
                },
                "evaluated_graph": _normalize_evaluated_graph(
                    item["evaluated_graph"]
                ),
                "implementation_assemblies": assemblies,
                "path": _relative_path(item["path"], "request project path"),
                "planning_build_props": {
                    "bytes": planning_build_props["bytes"],
                    "path": _relative_path(
                        planning_build_props["path"],
                        "request planning build props path",
                    ),
                    "sha256": _hash(
                        planning_build_props["sha256"],
                        "request planning build props hash",
                    ),
                },
                "restore_arguments": [
                    _text(argument, "request project restore argument")
                    for argument in _sequence(
                        item["restore_arguments"],
                        "request project restore arguments",
                    )
                ],
                "slug": _pattern_text(item["slug"], r"[a-z0-9]+(?:-[a-z0-9]+)*", "project slug"),
            }
        )
    paths = [item["path"] for item in result]
    if paths != sorted(set(paths)):
        raise TrustedCollectorError("request projects must be unique and sorted")
    for project in result:
        graph = project["evaluated_graph"]
        if graph["root_project"] != project["path"]:
            raise TrustedCollectorError("evaluated graph root does not match test project")
        root_metadata = next(
            (item for item in graph["projects"] if item["path"] == project["path"]),
            None,
        )
        if root_metadata is None or root_metadata["assembly_name"] != project["assembly_name"]:
            raise TrustedCollectorError("evaluated graph test assembly binding is stale")
    return result


def _plain_string(value: Any, context: str) -> str:
    if not isinstance(value, str):
        raise TrustedCollectorError(f"{context} must be text")
    return value


def _normalize_evaluated_graph(value: Any) -> Mapping[str, Any]:
    item = _mapping(value, "evaluated project graph")
    _exact_keys(
        item,
        {"projects", "root_project", "schema", "sha256", "target_framework"},
        "evaluated project graph",
    )
    if item["schema"] != EVALUATED_GRAPH_SCHEMA:
        raise TrustedCollectorError("evaluated project graph schema is invalid")
    projects: list[Mapping[str, Any]] = []
    for raw_project in _sequence(item["projects"], "evaluated projects"):
        project = _mapping(raw_project, "evaluated project")
        _exact_keys(
            project,
            {
                "assembly_name",
                "compile",
                "package_references",
                "path",
                "project_references",
            },
            "evaluated project",
        )
        compile_items: list[Mapping[str, str]] = []
        for raw_compile in _sequence(project["compile"], "evaluated Compile items"):
            compile_item = _mapping(raw_compile, "evaluated Compile item")
            _exact_keys(
                compile_item,
                {"defining_project", "link", "path"},
                "evaluated Compile item",
            )
            compile_items.append(
                {
                    "defining_project": _plain_string(
                        compile_item["defining_project"],
                        "evaluated Compile defining project",
                    ),
                    "link": _plain_string(compile_item["link"], "evaluated Compile link"),
                    "path": _relative_path(
                        compile_item["path"], "evaluated Compile path"
                    ),
                }
            )
        if compile_items != sorted(
            compile_items,
            key=lambda entry: (
                entry["path"], entry["link"], entry["defining_project"]
            ),
        ) or len({entry["path"] for entry in compile_items}) != len(compile_items):
            raise TrustedCollectorError("evaluated Compile items are not exact and sorted")
        references: list[Mapping[str, str]] = []
        for raw_reference in _sequence(
            project["project_references"], "evaluated ProjectReference items"
        ):
            reference = _mapping(raw_reference, "evaluated ProjectReference item")
            _exact_keys(
                reference,
                {"defining_project", "path"},
                "evaluated ProjectReference item",
            )
            references.append(
                {
                    "defining_project": _plain_string(
                        reference["defining_project"],
                        "evaluated ProjectReference defining project",
                    ),
                    "path": _relative_path(
                        reference["path"], "evaluated ProjectReference path"
                    ),
                }
            )
        if references != sorted(
            references, key=lambda entry: (entry["path"], entry["defining_project"])
        ) or len({entry["path"] for entry in references}) != len(references):
            raise TrustedCollectorError("evaluated ProjectReference items are not exact and sorted")
        packages: list[Mapping[str, str]] = []
        for raw_package in _sequence(
            project["package_references"], "evaluated PackageReference items"
        ):
            package = _mapping(raw_package, "evaluated PackageReference item")
            _exact_keys(
                package,
                {
                    "defining_project",
                    "exclude_assets",
                    "identity",
                    "private_assets",
                    "version",
                },
                "evaluated PackageReference item",
            )
            packages.append(
                {
                    key: _plain_string(
                        package[key], f"evaluated PackageReference {key}"
                    )
                    for key in (
                        "defining_project",
                        "exclude_assets",
                        "identity",
                        "private_assets",
                        "version",
                    )
                }
            )
        if packages != sorted(
            packages,
            key=lambda entry: (
                entry["identity"].casefold(),
                entry["version"],
                entry["defining_project"],
            ),
        ):
            raise TrustedCollectorError("evaluated PackageReference items are not sorted")
        projects.append(
            {
                "assembly_name": _pattern_text(
                    project["assembly_name"],
                    r"[A-Za-z_][A-Za-z0-9_.-]*",
                    "evaluated assembly name",
                ),
                "compile": compile_items,
                "package_references": packages,
                "path": _relative_path(project["path"], "evaluated project path"),
                "project_references": references,
            }
        )
    paths = [project["path"] for project in projects]
    if not projects or paths != sorted(set(paths)):
        raise TrustedCollectorError("evaluated projects must be nonempty, unique, and sorted")
    target_framework = _pattern_text(
        item["target_framework"],
        r"net[0-9]+\.[0-9]+-windows",
        "evaluated target framework",
    )
    assembly_names = [project["assembly_name"] for project in projects]
    if len(assembly_names) != len(set(assembly_names)):
        raise TrustedCollectorError("evaluated project graph has duplicate assembly names")
    content = {
        "projects": projects,
        "root_project": _relative_path(
            item["root_project"], "evaluated root project"
        ),
        "target_framework": target_framework,
    }
    expected = _sha256_data(content)
    if _hash(item["sha256"], "evaluated project graph hash") != expected:
        raise TrustedCollectorError("evaluated project graph hash is invalid")
    return {**content, "schema": EVALUATED_GRAPH_SCHEMA, "sha256": expected}


def _normalize_identifiers(value: Any, context: str) -> list[str]:
    result = [_identifier(item, context) for item in _sequence(value, context)]
    if result != sorted(set(result)):
        raise TrustedCollectorError(f"{context} must be unique and sorted")
    return result


def _msbuild_isolation_properties(
    source_root: Path,
    source_paths: set[str],
    root_project: str,
    build_props: Path,
    target_framework: str | None,
    sdk_root: Path,
    packages_root: Path,
    user_extensions_root: Path,
) -> list[str]:
    directory_build_targets_relative = _nearest_tracked_project_file(
        root_project,
        source_paths,
        "Directory.Build.targets",
    )
    directory_packages_props_relative = _nearest_tracked_project_file(
        root_project,
        source_paths,
        "Directory.Packages.props",
    )
    properties = [
        "-p:Configuration=Release",
    ]
    if target_framework is not None:
        properties.append(f"-p:TargetFramework={target_framework}")
    properties.extend(
        (
            "-p:ContinuousIntegrationBuild=true",
            "-p:Deterministic=true",
            "-p:RestoreLockedMode=true",
            "-p:RestorePackagesWithLockFile=true",
            "-p:RestoreNoCache=true",
            "-p:RestoreDisableParallel=true",
            "-p:NuGetAudit=false",
            f"-p:RestoreConfigFile={source_root / 'NuGet.config'}",
            f"-p:RestorePackagesPath={packages_root}",
            f"-p:MSBuildSDKsPath={sdk_root / 'Sdks'}",
            f"-p:MSBuildUserExtensionsPath={user_extensions_root}",
            (
                "-p:CustomBeforeMicrosoftCommonTargets="
                f"{source_root / '.goniegonie-no-custom-before.targets'}"
            ),
            (
                "-p:CustomAfterMicrosoftCommonTargets="
                f"{source_root / '.goniegonie-no-custom-after.targets'}"
            ),
            "-p:ImportDirectoryBuildProps=true",
            f"-p:DirectoryBuildPropsPath={build_props}",
        )
    )
    if directory_build_targets_relative is None:
        properties.append("-p:ImportDirectoryBuildTargets=false")
    else:
        properties.extend(
            (
                "-p:ImportDirectoryBuildTargets=true",
                "-p:DirectoryBuildTargetsPath="
                + str(
                    source_root.joinpath(
                        *PurePosixPath(directory_build_targets_relative).parts
                    )
                ),
            )
        )
    if directory_packages_props_relative is None:
        properties.append("-p:ImportDirectoryPackagesProps=false")
    else:
        properties.extend(
            (
                "-p:ImportDirectoryPackagesProps=true",
                "-p:DirectoryPackagesPropsPath="
                + str(
                    source_root.joinpath(
                        *PurePosixPath(directory_packages_props_relative).parts
                    )
                ),
            )
        )
    return properties


def _run_msbuild_process(
    command: Sequence[str],
    *,
    cwd: Path,
    environment: Mapping[str, str],
    run_command: Callable[..., subprocess.CompletedProcess[bytes]],
    context: str,
) -> bytes:
    try:
        completed = run_command(
            list(command),
            cwd=cwd,
            env=dict(environment),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=1800,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exception:
        raise TrustedCollectorError(f"{context} failed to run: {exception}") from exception
    stdout = bytes(completed.stdout or b"")
    stderr = bytes(completed.stderr or b"")
    if len(stdout) > _MAX_CHILD_OUTPUT_BYTES or len(stderr) > _MAX_CHILD_OUTPUT_BYTES:
        raise TrustedCollectorError(f"{context} output exceeded the safety limit")
    if completed.returncode != 0:
        detail = stderr.decode("utf-8", errors="replace").strip()
        if not detail:
            detail = stdout.decode("utf-8", errors="replace").strip()
        raise TrustedCollectorError(
            f"{context} failed: {detail or completed.returncode}"
        )
    return stdout


def _source_manifest_path(
    value: Any,
    source_root: Path,
    source_paths_by_case: Mapping[str, str],
    context: str,
) -> str:
    path = Path(_text(value, context))
    if not path.is_absolute():
        raise TrustedCollectorError(f"{context} must be absolute evaluated metadata")
    try:
        resolved = path.resolve(strict=True)
        relative = resolved.relative_to(source_root).as_posix()
    except (OSError, ValueError) as exception:
        raise TrustedCollectorError(f"{context} escaped the isolated source") from exception
    canonical = source_paths_by_case.get(relative.casefold())
    if canonical is None:
        raise TrustedCollectorError(f"{context} is not in the exact HEAD source manifest")
    exact = source_root.joinpath(*PurePosixPath(canonical).parts)
    _require_safe_ancestors(source_root, exact.parent)
    _require_regular_unlinked_file(exact, context)
    if exact.resolve(strict=True) != resolved:
        raise TrustedCollectorError(f"{context} has an ambiguous source path")
    return canonical


def _evaluated_defining_path(
    value: Any,
    *,
    source_root: Path,
    sdk_root: Path,
    packages_root: Path,
    session: Path,
    source_paths_by_case: Mapping[str, str],
    sdk_paths_by_case: Mapping[str, str],
    context: str,
) -> str:
    if value in (None, ""):
        return ""
    path = Path(_text(value, context))
    if not path.is_absolute():
        raise TrustedCollectorError(f"{context} must be an absolute path")
    resolved = path.resolve(strict=True)
    roots = (
        ("source", source_root, source_paths_by_case),
        ("sdk", sdk_root, sdk_paths_by_case),
    )
    for label, root, manifest in roots:
        try:
            relative = resolved.relative_to(root).as_posix()
        except ValueError:
            continue
        canonical = manifest.get(relative.casefold())
        if canonical is None:
            raise TrustedCollectorError(f"{context} is absent from the bound {label} manifest")
        return f"{label}:{canonical}"
    for label, root in (("nuget", packages_root), ("session", session)):
        try:
            relative = resolved.relative_to(root).as_posix()
        except ValueError:
            continue
        _require_safe_ancestors(root, resolved.parent)
        _require_regular_file(resolved, context)
        return f"{label}:{_relative_path(relative, context)}"
    raise TrustedCollectorError(f"{context} escaped all bound trusted roots")


def _evaluate_project_graph(
    source_root: Path,
    session: Path,
    root_project: str,
    slug: str,
    build_props: Mapping[str, Any],
    source_tree: Mapping[str, Any],
    target_framework: str,
    dotnet: Path,
    sdk_manifest: Mapping[str, Any],
    stage: str,
    *,
    run_command: Callable[..., subprocess.CompletedProcess[bytes]] = subprocess.run,
) -> Mapping[str, Any]:
    """Restore and query the exact evaluated project/item graph."""

    if re.fullmatch(r"g[0-9]+", stage) is None:
        raise TrustedCollectorError("evaluated MSBuild stage is invalid")
    source_paths = {item["path"] for item in source_tree["files"]}
    source_paths_by_case = {path.casefold(): path for path in source_paths}
    if len(source_paths_by_case) != len(source_paths):
        raise TrustedCollectorError("source manifest has case-ambiguous paths")
    sdk_paths = {item["path"] for item in sdk_manifest["files"]}
    sdk_paths_by_case = {path.casefold(): path for path in sdk_paths}
    if len(sdk_paths_by_case) != len(sdk_paths):
        raise TrustedCollectorError("SDK manifest has case-ambiguous paths")
    sdk_root = Path(sdk_manifest["root"])
    stage_root = session / stage / slug
    packages_root = session / "n" / stage / slug
    user_extensions_root = stage_root / "u"
    environment_root = stage_root / "e"
    for path in (packages_root, user_extensions_root):
        path.mkdir(parents=True, exist_ok=True)
        _require_safe_ancestors(session, path)
    environment = _isolated_dotnet_environment(environment_root, dotnet)
    environment["NUGET_PACKAGES"] = str(packages_root)
    build_props_path = _artifact_path(session, build_props, "evaluated build props")
    common = _msbuild_isolation_properties(
        source_root,
        source_paths,
        root_project,
        build_props_path,
        target_framework,
        sdk_root,
        packages_root,
        user_extensions_root,
    )
    restore_common = _msbuild_isolation_properties(
        source_root,
        source_paths,
        root_project,
        build_props_path,
        None,
        sdk_root,
        packages_root,
        user_extensions_root,
    )
    root_path = source_root.joinpath(*PurePosixPath(root_project).parts)
    _source_manifest_path(
        str(root_path), source_root, source_paths_by_case, "evaluated root project"
    )
    restore = [
        str(dotnet),
        "restore",
        "/noAutoResponse",
        str(root_path),
        "--locked-mode",
        "--configfile",
        str(source_root / "NuGet.config"),
        "--packages",
        str(packages_root),
        "--disable-build-servers",
        *restore_common,
    ]
    _run_msbuild_process(
        restore,
        cwd=source_root,
        environment=environment,
        run_command=run_command,
        context=f"locked restore for {root_project}",
    )

    pending = [root_project]
    projects: dict[str, Mapping[str, Any]] = {}
    while pending:
        project_path = pending.pop()
        if project_path in projects:
            continue
        absolute = source_root.joinpath(*PurePosixPath(project_path).parts)
        command = [
            str(dotnet),
            "msbuild",
            "/noAutoResponse",
            str(absolute),
            "-nologo",
            "-v:q",
            (
                "-getProperty:AssemblyName,TargetFramework,MSBuildProjectFullPath,"
                "NETCoreSdkVersion,MSBuildSDKsPath"
            ),
            "-getItem:ProjectReference,Compile,PackageReference",
            *common,
        ]
        raw = _run_msbuild_process(
            command,
            cwd=source_root,
            environment=environment,
            run_command=run_command,
            context=f"evaluated MSBuild metadata for {project_path}",
        )
        try:
            decoded = raw.decode("utf-8", errors="strict")
            stripped = decoded.strip()
            if not stripped.startswith("{") or not stripped.endswith("}"):
                raise ValueError("stdout is not one JSON object")
            value = _mapping(_json_loads(stripped), "evaluated MSBuild metadata")
        except (UnicodeError, ValueError, json.JSONDecodeError) as exception:
            raise TrustedCollectorError(
                f"evaluated MSBuild metadata for {project_path} is not exact JSON"
            ) from exception
        _exact_keys(value, {"Items", "Properties"}, "evaluated MSBuild metadata")
        properties = _mapping(value["Properties"], "evaluated MSBuild properties")
        _exact_keys(
            properties,
            {
                "AssemblyName",
                "MSBuildProjectFullPath",
                "MSBuildSDKsPath",
                "NETCoreSdkVersion",
                "TargetFramework",
            },
            "evaluated MSBuild properties",
        )
        assembly_name = _pattern_text(
            properties["AssemblyName"],
            r"[A-Za-z_][A-Za-z0-9_.-]*",
            "evaluated assembly name",
        )
        if properties["TargetFramework"] != target_framework:
            raise TrustedCollectorError("evaluated TargetFramework is not request-bound")
        if properties["NETCoreSdkVersion"] != sdk_root.name:
            raise TrustedCollectorError("evaluated SDK version is stale")
        evaluated_sdk_path = Path(_text(properties["MSBuildSDKsPath"], "evaluated SDK path"))
        if evaluated_sdk_path.resolve(strict=True) != (sdk_root / "Sdks").resolve(strict=True):
            raise TrustedCollectorError("evaluated MSBuild SDK path escaped the pinned SDK")
        actual_project = _source_manifest_path(
            properties["MSBuildProjectFullPath"],
            source_root,
            source_paths_by_case,
            "evaluated project path",
        )
        if actual_project != project_path:
            raise TrustedCollectorError("evaluated project path is inconsistent")
        items = _mapping(value["Items"], "evaluated MSBuild items")
        _exact_keys(
            items,
            {"Compile", "PackageReference", "ProjectReference"},
            "evaluated MSBuild items",
        )

        references: list[Mapping[str, str]] = []
        for raw_reference in _sequence(items["ProjectReference"], "evaluated ProjectReference"):
            reference = _mapping(raw_reference, "evaluated ProjectReference item")
            full_path = _source_manifest_path(
                reference.get("FullPath"),
                source_root,
                source_paths_by_case,
                "evaluated ProjectReference.FullPath",
            )
            defining = _evaluated_defining_path(
                reference.get("DefiningProjectFullPath"),
                source_root=source_root,
                sdk_root=sdk_root,
                packages_root=packages_root,
                session=session,
                source_paths_by_case=source_paths_by_case,
                sdk_paths_by_case=sdk_paths_by_case,
                context="evaluated ProjectReference defining project",
            )
            references.append({"defining_project": defining, "path": full_path})
        references.sort(key=lambda item: (item["path"], item["defining_project"]))
        if [item["path"] for item in references] != sorted(
            set(item["path"] for item in references)
        ):
            raise TrustedCollectorError("evaluated ProjectReference graph is ambiguous")

        compile_items: list[Mapping[str, str]] = []
        for raw_compile in _sequence(items["Compile"], "evaluated Compile"):
            compile_item = _mapping(raw_compile, "evaluated Compile item")
            full_path = _source_manifest_path(
                compile_item.get("FullPath"),
                source_root,
                source_paths_by_case,
                "evaluated Compile.FullPath",
            )
            defining = _evaluated_defining_path(
                compile_item.get("DefiningProjectFullPath"),
                source_root=source_root,
                sdk_root=sdk_root,
                packages_root=packages_root,
                session=session,
                source_paths_by_case=source_paths_by_case,
                sdk_paths_by_case=sdk_paths_by_case,
                context="evaluated Compile defining project",
            )
            link = compile_item.get("Link", "")
            if not isinstance(link, str):
                raise TrustedCollectorError("evaluated Compile Link is invalid")
            compile_items.append(
                {"defining_project": defining, "link": link, "path": full_path}
            )
        compile_items.sort(
            key=lambda item: (item["path"], item["link"], item["defining_project"])
        )
        if [item["path"] for item in compile_items] != sorted(
            set(item["path"] for item in compile_items)
        ):
            raise TrustedCollectorError("evaluated Compile inputs are ambiguous")

        package_items: list[Mapping[str, str]] = []
        for raw_package in _sequence(items["PackageReference"], "evaluated PackageReference"):
            package = _mapping(raw_package, "evaluated PackageReference item")
            identity = _text(package.get("Identity"), "evaluated package identity")
            defining = _evaluated_defining_path(
                package.get("DefiningProjectFullPath"),
                source_root=source_root,
                sdk_root=sdk_root,
                packages_root=packages_root,
                session=session,
                source_paths_by_case=source_paths_by_case,
                sdk_paths_by_case=sdk_paths_by_case,
                context="evaluated PackageReference defining project",
            )
            package_items.append(
                {
                    "defining_project": defining,
                    "exclude_assets": str(package.get("ExcludeAssets", "")),
                    "identity": identity,
                    "private_assets": str(package.get("PrivateAssets", "")),
                    "version": str(
                        package.get("VersionOverride") or package.get("Version") or ""
                    ),
                }
            )
        package_items.sort(
            key=lambda item: (
                item["identity"].casefold(),
                item["version"],
                item["defining_project"],
            )
        )
        projects[project_path] = {
            "assembly_name": assembly_name,
            "compile": compile_items,
            "package_references": package_items,
            "path": project_path,
            "project_references": references,
        }
        pending.extend(item["path"] for item in reversed(references))

    evaluated_assembly_names = [
        projects[path]["assembly_name"] for path in sorted(projects)
    ]
    if len(evaluated_assembly_names) != len(set(evaluated_assembly_names)):
        raise TrustedCollectorError("evaluated project graph has duplicate assembly names")
    content = {
        "projects": [projects[path] for path in sorted(projects)],
        "root_project": root_project,
        "target_framework": target_framework,
    }
    return {
        **content,
        "schema": EVALUATED_GRAPH_SCHEMA,
        "sha256": _sha256_data(content),
    }


def _build_collection_plan(
    source_root: Path,
    session: Path,
    registry: Mapping[str, Any],
    required: Sequence[str],
    target_framework: str,
    head_paths: Mapping[str, str],
    source_tree: Mapping[str, Any],
    dotnet: Path,
    sdk_manifest: Mapping[str, Any],
) -> tuple[tuple[Mapping[str, Any], ...], set[str]]:
    receipt_entries: dict[str, tuple[Mapping[str, Any], Mapping[str, Any]]] = {}
    for raw_entry in _sequence(registry["entries"], "canonical evidence entries"):
        entry = _mapping(raw_entry, "canonical evidence entry")
        implementation = _mapping(
            entry["implementation"], "canonical evidence implementation"
        )
        _relative_path(
            implementation["path"], "canonical evidence implementation path"
        )
        for raw_receipt in _sequence(entry["receipts"], "canonical evidence receipts"):
            receipt = _mapping(raw_receipt, "canonical evidence receipt")
            identifier = _identifier(receipt["id"], "canonical receipt id")
            if identifier in receipt_entries:
                raise TrustedCollectorError("canonical evidence receipt ids are duplicated")
            receipt_entries[identifier] = (receipt, entry)
    grouped: dict[
        str,
        list[tuple[Mapping[str, Any], Mapping[str, Any]]],
    ] = {}
    input_paths: set[str] = set()
    implementation_projects: set[str] = set()
    for identifier in required:
        receipt, entry = receipt_entries[identifier]
        implementation = _mapping(
            entry["implementation"], "canonical evidence implementation"
        )
        test_path = _relative_path(receipt["test_path"], "canonical receipt test path")
        implementation_path = _relative_path(
            implementation["path"], "canonical implementation path"
        )
        test_project = _find_owning_project(source_root, test_path)
        implementation_project = _find_owning_project(
            source_root, implementation_path
        )
        grouped.setdefault(test_project, []).append((receipt, entry))
        implementation_projects.add(implementation_project)
        input_paths.update(
            {test_path, implementation_path, test_project, implementation_project}
        )

    projects: list[Mapping[str, Any]] = []
    for project_path in sorted(grouped):
        assertions = []
        slug = _project_slug(project_path)
        skeleton = {"path": project_path, "slug": slug}
        build_props = _write_project_build_props(
            session,
            source_root,
            skeleton,
            source_tree,
        )
        evaluation_props = _write_project_build_props(
            session,
            source_root,
            skeleton,
            source_tree,
            stage="g0",
        )
        evaluation = _evaluate_project_graph(
            source_root,
            session,
            project_path,
            slug,
            evaluation_props,
            source_tree,
            target_framework,
            dotnet,
            sdk_manifest,
            "g0",
        )
        evaluated_projects = {
            item["path"]: item for item in evaluation["projects"]
        }
        if project_path not in evaluated_projects:
            raise TrustedCollectorError("evaluated graph omitted its root test project")
        implementation_names: set[str] = set()
        for receipt, entry in sorted(
            grouped[project_path], key=lambda pair: pair[0]["id"]
        ):
            implementation = _mapping(
                entry["implementation"], "canonical evidence implementation"
            )
            test_path = _relative_path(
                receipt["test_path"], "canonical receipt test path"
            )
            implementation_path = _relative_path(
                implementation["path"], "canonical implementation path"
            )
            test_compile = {
                item["path"]
                for item in evaluated_projects[project_path]["compile"]
            }
            if test_path not in test_compile:
                raise TrustedCollectorError(
                    f"evidence test is not an evaluated Compile item: {test_path}"
                )
            assertions.append(
                {
                    "exercised_load": _text(
                        receipt["exercised_load"], "canonical receipt exercised load"
                    ),
                    "id": _identifier(receipt["id"], "canonical receipt id"),
                    "test_path": test_path,
                    "test_source_sha256": _hash(
                        receipt["test_source_sha256"],
                        "canonical receipt test source hash",
                    ),
                    "test_symbol": _symbol(
                        receipt["test_symbol"], "canonical receipt test symbol"
                    ),
                }
            )
            implementation_project = _find_owning_project(
                source_root, implementation_path
            )
            metadata = evaluated_projects.get(implementation_project)
            if metadata is None:
                raise TrustedCollectorError(
                    "evidence implementation project is not in the evaluated test graph"
                )
            if implementation_path not in {
                item["path"] for item in metadata["compile"]
            }:
                raise TrustedCollectorError(
                    "evidence implementation is not an evaluated Compile item"
                )
            pending = [implementation_project]
            visited: set[str] = set()
            while pending:
                graph_project = pending.pop()
                if graph_project in visited:
                    continue
                visited.add(graph_project)
                graph_metadata = evaluated_projects[graph_project]
                implementation_names.add(graph_metadata["assembly_name"])
                pending.extend(
                    item["path"]
                    for item in graph_metadata["project_references"]
                )
        for metadata in evaluated_projects.values():
            input_paths.add(metadata["path"])
            input_paths.update(item["path"] for item in metadata["compile"])
            project_dir = PurePosixPath(metadata["path"]).parent.as_posix()
            prefix = f"{project_dir}/" if project_dir != "." else ""
            lock_path = f"{prefix}packages.lock.json"
            if lock_path not in head_paths:
                raise TrustedCollectorError(
                    "trusted evidence project requires tracked packages.lock.json: "
                    + metadata["path"]
                )
            input_paths.add(lock_path)
        projects.append(
            {
                "assembly_name": evaluated_projects[project_path]["assembly_name"],
                "assertions": assertions,
                "build_props": build_props,
                "evaluated_graph": evaluation,
                "implementation_assemblies": sorted(implementation_names),
                "path": project_path,
                "planning_build_props": evaluation_props,
                "slug": slug,
            }
        )
    input_paths.update(
        path
        for path in head_paths
        if (
            PurePosixPath(path).name == "packages.lock.json"
            or PurePosixPath(path).name
            in {
                "Directory.Build.props",
                "Directory.Build.targets",
                "Directory.Packages.props",
                "Directory.Packages.targets",
                "NuGet.config",
            }
        )
    )
    return tuple(projects), input_paths


def _find_owning_project(root: Path, source_path: str) -> str:
    source = root.joinpath(*source_path.split("/"))
    if not source.is_file():
        raise TrustedCollectorError(f"evidence source does not exist: {source_path}")
    current = source.parent
    while current != root.parent:
        candidates = tuple(current.glob("*.csproj"))
        if len(candidates) == 1:
            return candidates[0].relative_to(root).as_posix()
        if len(candidates) > 1:
            raise TrustedCollectorError(f"evidence source has ambiguous owning project: {source_path}")
        if current == root:
            break
        current = current.parent
    raise TrustedCollectorError(f"evidence source has no owning .NET project: {source_path}")


def _project_reference_closure(root: Path, initial: str) -> set[str]:
    pending = [initial]
    result: set[str] = set()
    while pending:
        project = pending.pop()
        if project in result:
            continue
        result.add(project)
        path = root.joinpath(*project.split("/"))
        tree = _parse_trusted_msbuild_xml(path, f"project '{project}'")
        for reference in tree.iter():
            if reference.tag.rsplit("}", 1)[-1] != "ProjectReference":
                continue
            include = reference.get("Include")
            if not include or "$" in include or "*" in include:
                raise TrustedCollectorError(f"project has non-exact ProjectReference: {project}")
            resolved = (path.parent / Path(include.replace("\\", os.sep))).resolve(strict=True)
            try:
                relative = _relative_path(
                    resolved.relative_to(root).as_posix(),
                    "ProjectReference path",
                )
            except ValueError as exception:
                raise TrustedCollectorError("ProjectReference escaped the repository") from exception
            pending.append(relative)
    return result


def _project_explicit_compile_inputs(root: Path, project: str) -> set[str]:
    path = root.joinpath(*project.split("/"))
    tree = _parse_trusted_msbuild_xml(path, f"project '{project}'")
    result: set[str] = set()
    for element in tree.iter():
        if element.tag.rsplit("}", 1)[-1] != "Compile":
            continue
        include = element.get("Include")
        if include is None:
            continue
        if "$" in include or "*" in include or "?" in include:
            raise TrustedCollectorError(f"project has non-exact Compile Include: {project}")
        resolved = (path.parent / Path(include.replace("\\", os.sep))).resolve(strict=True)
        try:
            relative = _relative_path(
                resolved.relative_to(root).as_posix(),
                "Compile Include path",
            )
        except ValueError as exception:
            raise TrustedCollectorError("Compile Include escaped the repository") from exception
        if not resolved.is_file():
            raise TrustedCollectorError(f"Compile Include is not an exact file: {project}")
        result.add(relative)
    return result


def _project_assembly_name(root: Path, project: str) -> str:
    path = root.joinpath(*project.split("/"))
    tree = _parse_trusted_msbuild_xml(path, f"project '{project}'")
    names = [
        (element.text or "").strip()
        for element in tree.iter()
        if element.tag.rsplit("}", 1)[-1] == "AssemblyName" and (element.text or "").strip()
    ]
    if len(set(names)) > 1 or any("$" in item for item in names):
        raise TrustedCollectorError(f"project has a conditional or dynamic AssemblyName: {project}")
    return names[0] if names else path.stem


def _parse_trusted_msbuild_xml(path: Path, context: str) -> ElementTree.Element:
    """Parse tracked MSBuild XML without permitting DTD/entity expansion.

    ElementTree does not fetch external DTDs by default, but declarations are
    rejected explicitly so the accepted grammar is identical across Python
    versions and every csproj/props/targets inspection fails closed.
    """

    try:
        raw = path.read_bytes()
    except OSError as exception:
        raise TrustedCollectorError(f"cannot read {context}: {exception}") from exception
    if re.search(br"<!\s*(?:DOCTYPE|ENTITY)\b", raw, flags=re.IGNORECASE):
        raise TrustedCollectorError(
            f"{context} must not contain DOCTYPE or ENTITY declarations"
        )
    try:
        return ElementTree.fromstring(raw)
    except ElementTree.ParseError as exception:
        raise TrustedCollectorError(f"cannot parse {context}: {exception}") from exception


def _verify_msbuild_xml_sources(
    source_root: Path,
    source_tree: Mapping[str, Any],
) -> None:
    for item in source_tree["files"]:
        relative = item["path"]
        if not relative.casefold().endswith((".csproj", ".props", ".targets")):
            continue
        path = source_root.joinpath(*PurePosixPath(relative).parts)
        _require_safe_ancestors(source_root, path.parent)
        _parse_trusted_msbuild_xml(path, f"tracked MSBuild XML '{relative}'")


def _project_slug(project: str) -> str:
    return hashlib.sha256(project.encode("utf-8")).hexdigest()[:12]


def _load_pinned_sdk(path: Path) -> str:
    try:
        value = _json_loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exception:
        raise TrustedCollectorError(f"cannot read pinned SDK from '{path}'") from exception
    root = _mapping(value, "global.json")
    _exact_keys(root, {"sdk"}, "global.json")
    sdk = _mapping(root["sdk"], "global.json sdk")
    _exact_keys(sdk, {"allowPrerelease", "rollForward", "version"}, "global.json sdk")
    version = _text(sdk["version"], "global.json SDK version")
    if (
        _SDK_VERSION.fullmatch(version) is None
        or sdk["rollForward"] != "disable"
        or sdk["allowPrerelease"] is not False
    ):
        raise TrustedCollectorError("global.json must pin one stable SDK with rollForward disabled")
    return version


def _resolve_dotnet(
    root: Path,
    expected_version: str,
    environment_root: Path,
) -> tuple[Path, Path]:
    local_name = "dotnet.exe" if os.name == "nt" else "dotnet"
    local = root / ".tools" / "dotnet" / local_name
    candidate = str(local) if local.is_file() else shutil.which("dotnet")
    if candidate is None:
        raise TrustedCollectorError("dotnet is unavailable for trusted evidence collection")
    path = Path(candidate).resolve(strict=True)
    sdk_root = _verify_dotnet_version(
        root,
        path,
        expected_version,
        subprocess.run,
        environment_root,
    )
    return path, sdk_root


def _verify_dotnet_version(
    root: Path,
    dotnet: Path,
    expected_version: str,
    run_command: Callable[..., subprocess.CompletedProcess[bytes]],
    environment_root: Path,
) -> Path:
    environment = _isolated_dotnet_environment(environment_root, dotnet)
    try:
        completed = run_command(
            [str(dotnet), "--version"],
            cwd=root,
            env=environment,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=30,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exception:
        raise TrustedCollectorError(f"cannot inspect dotnet SDK: {exception}") from exception
    actual = bytes(completed.stdout or b"").decode("utf-8", errors="strict").strip()
    if completed.returncode != 0 or actual != expected_version:
        raise TrustedCollectorError(
            f"dotnet SDK must be exactly {expected_version}; found {actual or 'unavailable'}"
        )
    try:
        listed = run_command(
            [str(dotnet), "--list-sdks"],
            cwd=root,
            env=environment,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=30,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exception:
        raise TrustedCollectorError(f"cannot inspect dotnet SDK root: {exception}") from exception
    if listed.returncode != 0:
        raise TrustedCollectorError("dotnet --list-sdks failed")
    matches: list[Path] = []
    lines = bytes(listed.stdout or b"").decode("utf-8", errors="strict").splitlines()
    for line in lines:
        match = re.fullmatch(r"([^\s]+)\s+\[(.+)\]", line.strip())
        if match is not None and match.group(1) == expected_version:
            matches.append((Path(match.group(2)) / expected_version).resolve(strict=True))
    if len(matches) != 1:
        raise TrustedCollectorError(
            f"dotnet SDK root for {expected_version} is missing or ambiguous"
        )
    return matches[0]


def _sdk_toolchain_manifest(sdk_root: Path) -> Mapping[str, Any]:
    """Hash every regular file in the pinned SDK directory.

    A complete SDK-root manifest is intentionally broader than a hand-picked
    list of MSBuild/NuGet DLLs and props/targets. SDK imports can change across
    patch releases, so the closed file set is both simpler and safer.
    """

    sdk_root = sdk_root.resolve(strict=True)
    _require_directory(sdk_root, "pinned SDK root")
    files: list[Mapping[str, Any]] = []
    for directory, directory_names, file_names in os.walk(sdk_root):
        directory_path = Path(directory)
        _require_safe_ancestors(sdk_root, directory_path)
        directory_names.sort()
        file_names.sort()
        for name in file_names:
            path = directory_path / name
            _require_safe_ancestors(sdk_root, path.parent)
            _require_regular_file(path, "pinned SDK file")
            relative = _relative_path(
                path.relative_to(sdk_root).as_posix(),
                "pinned SDK file path",
            )
            files.append(
                {
                    "bytes": path.stat().st_size,
                    "path": relative,
                    "sha256": _sha256_file(path),
                }
            )
    files.sort(key=lambda item: item["path"])
    paths = [item["path"] for item in files]
    if not files or paths != sorted(set(paths)):
        raise TrustedCollectorError("pinned SDK manifest is empty or ambiguous")
    content = {"files": files, "root": sdk_root.as_posix()}
    return {
        **content,
        "file_count": len(files),
        "schema": SDK_MANIFEST_SCHEMA,
        "sha256": _sha256_data(content),
    }


def _normalize_sdk_manifest(value: Any, sdk_root: Path) -> Mapping[str, Any]:
    item = _mapping(value, "request SDK manifest")
    _exact_keys(
        item,
        {"file_count", "files", "root", "schema", "sha256"},
        "request SDK manifest",
    )
    if item["schema"] != SDK_MANIFEST_SCHEMA:
        raise TrustedCollectorError("request SDK manifest schema is invalid")
    root = Path(_text(item["root"], "request SDK manifest root"))
    if not root.is_absolute() or root.resolve(strict=True) != sdk_root.resolve(strict=True):
        raise TrustedCollectorError("request SDK manifest root is stale")
    files: list[Mapping[str, Any]] = []
    for raw in _sequence(item["files"], "request SDK manifest files"):
        entry = _mapping(raw, "request SDK manifest file")
        _exact_keys(entry, {"bytes", "path", "sha256"}, "request SDK manifest file")
        size = entry["bytes"]
        if not isinstance(size, int) or isinstance(size, bool) or size < 0:
            raise TrustedCollectorError("request SDK manifest file size is invalid")
        files.append(
            {
                "bytes": size,
                "path": _relative_path(entry["path"], "request SDK manifest file path"),
                "sha256": _hash(entry["sha256"], "request SDK manifest file hash"),
            }
        )
    paths = [entry["path"] for entry in files]
    if not files or paths != sorted(set(paths)):
        raise TrustedCollectorError("request SDK manifest files must be nonempty, unique, and sorted")
    count = item["file_count"]
    if not isinstance(count, int) or isinstance(count, bool) or count != len(files):
        raise TrustedCollectorError("request SDK manifest file count is invalid")
    content = {"files": files, "root": root.resolve(strict=True).as_posix()}
    expected_hash = _sha256_data(content)
    if _hash(item["sha256"], "request SDK manifest hash") != expected_hash:
        raise TrustedCollectorError("request SDK manifest aggregate hash is invalid")
    return {
        **content,
        "file_count": count,
        "schema": SDK_MANIFEST_SCHEMA,
        "sha256": expected_hash,
    }


def _verify_sdk_toolchain_manifest(expected: Mapping[str, Any]) -> None:
    actual = _sdk_toolchain_manifest(Path(expected["root"]))
    if actual != expected:
        raise TrustedCollectorError("pinned SDK toolchain changed during trusted collection")


def _load_canonical_evidence_manifests(
    root: Path,
) -> tuple[
    Mapping[str, Any],
    Mapping[str, Any],
    Mapping[str, Any],
    tuple[str, ...],
]:
    try:
        inventory = _mapping(
            _json_loads(
                (root / "upstream" / "public-symbol-inventory.json").read_bytes()
            ),
            "canonical public symbol inventory",
        )
        evidence = _mapping(
            _json_loads((root / "upstream" / "symbol-evidence.json").read_bytes()),
            "canonical symbol evidence registry",
        )
        matrix = _mapping(
            _json_loads(
                (root / "upstream" / "compatibility-matrix.json").read_bytes()
            ),
            "canonical compatibility matrix",
        )
    except (OSError, UnicodeError, json.JSONDecodeError) as exception:
        raise TrustedCollectorError("cannot load canonical evidence manifests") from exception
    binding = {
        "inventory_sha256": _hash(
            inventory.get("content_sha256"), "canonical inventory content hash"
        ),
        "matrix_sha256": _hash(
            matrix.get("content_sha256"), "canonical compatibility matrix hash"
        ),
        "symbol_evidence_sha256": _hash(
            evidence.get("content_sha256"), "canonical symbol evidence content hash"
        ),
        "upstream_commit": _text(
            inventory.get("upstream_commit"), "canonical upstream commit"
        ),
    }
    _verify_canonical_evidence_binding(root, binding)
    required = _canonical_required_assertion_ids(inventory, evidence, matrix)
    return inventory, evidence, matrix, required


def _canonical_receipt_ids(evidence: Mapping[str, Any]) -> set[str]:
    identifiers: set[str] = set()
    for raw_entry in _sequence(evidence.get("entries"), "canonical evidence entries"):
        entry = _mapping(raw_entry, "canonical evidence entry")
        _mapping(entry.get("implementation"), "canonical evidence implementation")
        for raw_receipt in _sequence(
            entry.get("receipts"), "canonical evidence receipts"
        ):
            receipt = _mapping(raw_receipt, "canonical evidence receipt")
            identifier = _identifier(receipt.get("id"), "canonical evidence receipt id")
            if identifier in identifiers:
                raise TrustedCollectorError("canonical evidence receipt ids are duplicated")
            identifiers.add(identifier)
    return identifiers


def _canonical_required_assertion_ids(
    inventory: Mapping[str, Any],
    evidence: Mapping[str, Any],
    matrix: Mapping[str, Any],
) -> tuple[str, ...]:
    _exact_keys(
        matrix,
        {
            "classifications",
            "content_sha256",
            "details",
            "entry_order",
            "inventory_sha256",
            "needs_reverification_rationale",
            "schema",
            "summary",
            "upstream_commit",
        },
        "canonical compatibility matrix",
    )
    if matrix["schema"] != _MATRIX_SCHEMA or matrix["entry_order"] != "public-symbol-inventory.symbols":
        raise TrustedCollectorError("canonical compatibility matrix schema/order is invalid")
    matrix_content = {
        "classifications": matrix["classifications"],
        "details": matrix["details"],
        "entry_order": matrix["entry_order"],
        "inventory_sha256": matrix["inventory_sha256"],
        "needs_reverification_rationale": matrix["needs_reverification_rationale"],
        "upstream_commit": matrix["upstream_commit"],
    }
    if (
        _hash(matrix["content_sha256"], "canonical compatibility matrix hash")
        != _sha256_data(matrix_content)
        or matrix["inventory_sha256"] != inventory["content_sha256"]
        or matrix["upstream_commit"] != inventory["upstream_commit"]
    ):
        raise TrustedCollectorError("canonical compatibility matrix binding is stale")
    symbols = _sequence(inventory["symbols"], "canonical inventory symbols")
    classifications = _sequence(
        matrix["classifications"], "canonical matrix classifications"
    )
    if len(symbols) != len(classifications):
        raise TrustedCollectorError("canonical matrix does not exactly cover inventory symbols")
    evidence_by_key: dict[tuple[str, str], Mapping[str, Any]] = {}
    all_receipt_ids = _canonical_receipt_ids(evidence)
    for raw_entry in _sequence(evidence["entries"], "canonical evidence entries"):
        entry = _mapping(raw_entry, "canonical evidence entry")
        key = (
            _relative_path(entry["path"], "canonical evidence path"),
            _symbol(entry["symbol"], "canonical evidence symbol"),
        )
        if key in evidence_by_key:
            raise TrustedCollectorError("canonical evidence symbol keys are duplicated")
        evidence_by_key[key] = entry
    required: set[str] = set()
    allowed = {"equivalent", "exception", "needs_reverification", "out_of_scope"}
    for index, (raw_symbol, classification_value) in enumerate(
        zip(symbols, classifications)
    ):
        classification = _text(
            classification_value, f"canonical matrix classification[{index}]"
        )
        if classification not in allowed:
            raise TrustedCollectorError("canonical matrix classification is invalid")
        if classification not in {"equivalent", "exception"}:
            continue
        symbol = _mapping(raw_symbol, f"canonical inventory symbol[{index}]")
        key = (
            _relative_path(symbol["path"], "canonical inventory symbol path"),
            _symbol(symbol["symbol"], "canonical inventory symbol name"),
        )
        registered = evidence_by_key.get(key)
        if registered is None:
            raise TrustedCollectorError(
                "canonical equivalent/exception symbol lacks evidence"
            )
        for raw_receipt in _sequence(
            registered["receipts"], "canonical symbol receipts"
        ):
            receipt = _mapping(raw_receipt, "canonical symbol receipt")
            required.add(_identifier(receipt["id"], "canonical receipt id"))
    if not required.issubset(all_receipt_ids):
        raise TrustedCollectorError("canonical required assertion closure is inconsistent")
    return tuple(sorted(required))


def _require_canonical_manifest(path: Path, expected: Mapping[str, Any], context: str) -> None:
    try:
        actual = _json_loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exception:
        raise TrustedCollectorError(f"cannot read canonical {context}") from exception
    if actual != expected:
        raise TrustedCollectorError(f"canonical {context} does not match the in-memory registry")


def _verify_canonical_evidence_binding(
    source_root: Path,
    binding: Mapping[str, Any],
) -> None:
    """Recompute manifest content identities from the isolated HEAD bytes."""

    try:
        inventory = _mapping(
            _json_loads(
                (source_root / "upstream" / "public-symbol-inventory.json").read_bytes()
            ),
            "canonical public symbol inventory",
        )
        evidence = _mapping(
            _json_loads(
                (source_root / "upstream" / "symbol-evidence.json").read_bytes()
            ),
            "canonical symbol evidence registry",
        )
        matrix = _mapping(
            _json_loads(
                (source_root / "upstream" / "compatibility-matrix.json").read_bytes()
            ),
            "canonical compatibility matrix",
        )
    except (OSError, UnicodeError, json.JSONDecodeError) as exception:
        raise TrustedCollectorError(
            "cannot read canonical evidence manifests from isolated source"
        ) from exception
    _exact_keys(
        inventory,
        {
            "content_sha256",
            "files",
            "schema",
            "scope_sha256",
            "summary",
            "symbols",
            "upstream_commit",
        },
        "canonical public symbol inventory",
    )
    _exact_keys(
        evidence,
        {
            "content_sha256",
            "entries",
            "inventory_sha256",
            "schema",
            "summary",
            "upstream_commit",
        },
        "canonical symbol evidence registry",
    )
    _exact_keys(
        matrix,
        {
            "classifications",
            "content_sha256",
            "details",
            "entry_order",
            "inventory_sha256",
            "needs_reverification_rationale",
            "schema",
            "summary",
            "upstream_commit",
        },
        "canonical compatibility matrix",
    )
    if (
        inventory["schema"] != _INVENTORY_SCHEMA
        or evidence["schema"] != _EVIDENCE_SCHEMA
        or matrix["schema"] != _MATRIX_SCHEMA
    ):
        raise TrustedCollectorError("canonical evidence manifest schema is invalid")
    upstream_commit = _text(inventory["upstream_commit"], "inventory upstream commit")
    if _COMMIT.fullmatch(upstream_commit) is None:
        raise TrustedCollectorError("canonical inventory upstream commit is invalid")
    inventory_content_hash = _sha256_data(
        {
            "files": inventory["files"],
            "scope_sha256": inventory["scope_sha256"],
            "symbols": inventory["symbols"],
            "upstream_commit": upstream_commit,
        }
    )
    evidence_content_hash = _sha256_data(
        {
            "entries": evidence["entries"],
            "inventory_sha256": evidence["inventory_sha256"],
            "upstream_commit": evidence["upstream_commit"],
        }
    )
    matrix_content_hash = _sha256_data(
        {
            "classifications": matrix["classifications"],
            "details": matrix["details"],
            "entry_order": matrix["entry_order"],
            "inventory_sha256": matrix["inventory_sha256"],
            "needs_reverification_rationale": matrix[
                "needs_reverification_rationale"
            ],
            "upstream_commit": matrix["upstream_commit"],
        }
    )
    if (
        _hash(inventory["content_sha256"], "inventory content hash")
        != inventory_content_hash
        or _hash(evidence["content_sha256"], "symbol evidence content hash")
        != evidence_content_hash
        or evidence["inventory_sha256"] != inventory_content_hash
        or evidence["upstream_commit"] != upstream_commit
        or _hash(matrix["content_sha256"], "matrix content hash")
        != matrix_content_hash
        or matrix["inventory_sha256"] != inventory_content_hash
        or matrix["upstream_commit"] != upstream_commit
        or binding["inventory_sha256"] != inventory_content_hash
        or binding["matrix_sha256"] != matrix_content_hash
        or binding["symbol_evidence_sha256"] != evidence_content_hash
        or binding["upstream_commit"] != upstream_commit
    ):
        raise TrustedCollectorError(
            "request evidence binding does not match the canonical isolated manifests"
        )


def _materialize_source_tree(
    repository_root: Path,
    source_root: Path,
    tracked_hashes: Mapping[str, str],
) -> Mapping[str, Any]:
    if source_root.exists():
        raise TrustedCollectorError("materialized source directory already exists")
    source_root.mkdir(mode=0o700)
    _require_safe_ancestors(source_root.parent, source_root)
    files: list[Mapping[str, str]] = []
    for raw_relative, expected_hash in sorted(tracked_hashes.items()):
        relative = _relative_path(raw_relative, "tracked source path")
        if ".git" in {part.casefold() for part in PurePosixPath(relative).parts}:
            raise TrustedCollectorError("tracked source tree must not contain .git")
        source = repository_root.joinpath(*PurePosixPath(relative).parts)
        _require_regular_unlinked_file(source, f"tracked source {relative}")
        value = source.read_bytes()
        if _sha256_bytes(value) != expected_hash:
            raise TrustedCollectorError(f"tracked source changed during materialization: {relative}")
        target = source_root.joinpath(*PurePosixPath(relative).parts)
        target.parent.mkdir(parents=True, exist_ok=True)
        _require_safe_ancestors(source_root, target.parent)
        _write_exclusive(target, value)
        files.append({"path": relative, "sha256": expected_hash})
    descriptor = {
        "file_count": len(files),
        "files": files,
        "sha256": _sha256_data({"files": files}),
    }
    _verify_materialized_source(source_root, descriptor)
    return descriptor


def _verify_materialized_source(
    source_root: Path,
    descriptor: Mapping[str, Any],
) -> None:
    _require_directory(source_root, "materialized source root")
    expected_files = {
        item["path"]: item["sha256"]
        for item in descriptor["files"]
    }
    expected_directories = {""}
    for relative in expected_files:
        parent = PurePosixPath(relative).parent
        while parent.as_posix() != ".":
            expected_directories.add(parent.as_posix())
            parent = parent.parent
    actual_files: dict[str, str] = {}
    actual_directories = {""}
    for current_text, directories, files in os.walk(source_root, followlinks=False):
        current = Path(current_text)
        _reject_reparse(current, "materialized source directory")
        current_relative = current.relative_to(source_root).as_posix()
        if current_relative == ".":
            current_relative = ""
        actual_directories.add(current_relative)
        for name in tuple(directories):
            child = current / name
            _reject_reparse(child, "materialized source directory")
            relative = child.relative_to(source_root).as_posix()
            _relative_path(f"{relative}/placeholder.file", "materialized source directory")
            actual_directories.add(relative)
        for name in files:
            path = current / name
            relative = _relative_path(
                path.relative_to(source_root).as_posix(),
                "materialized source file",
            )
            if ".git" in {part.casefold() for part in PurePosixPath(relative).parts}:
                raise TrustedCollectorError("materialized source contains forbidden .git data")
            _require_regular_unlinked_file(path, f"materialized source file {relative}")
            actual_files[relative] = _sha256_file(path)
    if actual_directories != expected_directories:
        raise TrustedCollectorError("materialized source contains missing or extra directories")
    if actual_files != expected_files:
        raise TrustedCollectorError("materialized source differs from the exact HEAD file set")
    files = [
        {"path": path, "sha256": actual_files[path]}
        for path in sorted(actual_files)
    ]
    if (
        descriptor["file_count"] != len(files)
        or descriptor["sha256"] != _sha256_data({"files": files})
    ):
        raise TrustedCollectorError("materialized source aggregate hash is invalid")


def _resolve_git() -> Path:
    candidate = shutil.which("git")
    if candidate is None:
        raise TrustedCollectorError("git is unavailable for trusted evidence collection")
    path = Path(candidate).resolve(strict=True)
    _require_regular_file(path, "git executable")
    return path


def _exact_repository_snapshot(
    root: Path,
    git_executable: Path | None = None,
    expected_git_sha256: str | None = None,
) -> tuple[str, Mapping[str, str]]:
    git = _resolve_git() if git_executable is None else git_executable.resolve(strict=True)
    _require_regular_file(git, "git executable")
    if expected_git_sha256 is not None and _sha256_file(git) != expected_git_sha256:
        raise TrustedCollectorError("git executable changed before repository inspection")
    environment = _git_environment()
    head = _git(git, root, ["rev-parse", "--verify", "HEAD"], environment).decode("ascii").strip().lower()
    if _COMMIT.fullmatch(head) is None:
        raise TrustedCollectorError("repository HEAD is not an exact commit")
    status = _git(
        git,
        root,
        ["status", "--porcelain=v2", "--untracked-files=all"],
        environment,
    )
    if status:
        raise TrustedCollectorError("trusted evidence requires an exact clean repository")
    flags = _git(git, root, ["ls-files", "-v", "-z"], environment)
    for raw in flags.split(b"\0"):
        if raw and chr(raw[0]).islower():
            raise TrustedCollectorError("trusted evidence rejects assume-unchanged files")
    staged = _git(git, root, ["ls-files", "--stage", "-z"], environment)
    paths: dict[str, tuple[str, str]] = {}
    for raw in staged.split(b"\0"):
        if not raw:
            continue
        metadata, raw_path = raw.split(b"\t", 1)
        mode, oid, stage = metadata.decode("ascii").split(" ")
        if stage != "0" or mode not in {"100644", "100755"}:
            raise TrustedCollectorError("trusted evidence rejects staged conflicts, links, and submodules")
        relative = _relative_path(
            raw_path.decode("utf-8", errors="strict"),
            "tracked repository path",
        )
        paths[relative] = (mode, oid)
    skip_worktree = _git(
        git,
        root,
        ["ls-files", "-t", "-z"],
        environment,
    )
    for raw in skip_worktree.split(b"\0"):
        if raw.startswith(b"S "):
            raise TrustedCollectorError("trusted evidence rejects skip-worktree files")
    hashes: dict[str, str] = {}
    for relative, (_, oid) in sorted(paths.items()):
        path = root.joinpath(*PurePosixPath(relative).parts)
        _require_regular_unlinked_file(path, f"tracked file {relative}")
        actual = path.read_bytes()
        expected = _git(git, root, ["cat-file", "blob", oid], environment)
        if actual != expected:
            raise TrustedCollectorError(
                f"tracked file differs byte-for-byte from HEAD: {relative}"
            )
        hashes[relative] = _sha256_bytes(actual)
    if expected_git_sha256 is not None and _sha256_file(git) != expected_git_sha256:
        raise TrustedCollectorError("git executable changed during repository inspection")
    return head, hashes


def _git(
    git_executable: Path,
    root: Path,
    arguments: Sequence[str],
    environment: Mapping[str, str],
) -> bytes:
    try:
        completed = subprocess.run(
            [
                str(git_executable),
                "--no-replace-objects",
                "-c",
                "core.fsmonitor=false",
                "-c",
                "core.untrackedCache=false",
                "-C",
                str(root),
                *arguments,
            ],
            env=dict(environment),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
    except OSError as exception:
        raise TrustedCollectorError(f"cannot inspect repository: {exception}") from exception
    if completed.returncode != 0:
        detail = completed.stderr.decode("utf-8", errors="replace").strip()
        raise TrustedCollectorError(f"cannot inspect repository: {detail or arguments[0]}")
    return completed.stdout


def _git_environment() -> Mapping[str, str]:
    allowed = {
        "COMSPEC",
        "OS",
        "PATHEXT",
        "SYSTEMDRIVE",
        "SYSTEMROOT",
        "WINDIR",
    }
    result = {
        key: value for key, value in os.environ.items() if key.upper() in allowed
    }
    result.update(
        {
            "GIT_CONFIG_GLOBAL": (
                "NUL" if os.name == "nt" else "/dev/null"
            ),
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_NO_REPLACE_OBJECTS": "1",
            "GIT_OPTIONAL_LOCKS": "0",
        }
    )
    return result


def _sanitized_environment() -> dict[str, str]:
    # MSBuild promotes every inherited environment variable to a property.
    # Copy only OS process essentials; in particular, do not inherit arbitrary
    # DirectoryBuild*Path, Custom*Targets, RestoreSources, MSBuild*, NuGet*, or
    # DOTNET_* values. The build-specific caller adds exact session paths.
    allowed = frozenset(
        {
            "COMSPEC",
            "NUMBER_OF_PROCESSORS",
            "OS",
            "PATHEXT",
            "PROCESSOR_ARCHITECTURE",
            "PROCESSOR_IDENTIFIER",
            "PROCESSOR_LEVEL",
            "PROCESSOR_REVISION",
            "SYSTEMDRIVE",
            "SYSTEMROOT",
            "WINDIR",
        }
    )
    result = {
        key: value for key, value in os.environ.items() if key.upper() in allowed
    }
    result.update(
        {
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_MULTILEVEL_LOOKUP": "0",
            "DOTNET_NOLOGO": "1",
            "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        }
    )
    return result


def _isolated_dotnet_environment(
    environment_root: Path,
    dotnet: Path | None = None,
) -> dict[str, str]:
    environment_root.mkdir(parents=True, exist_ok=True)
    _require_directory(environment_root, "isolated dotnet environment root")
    profile_root = environment_root / "home"
    if os.name == "nt":
        # NuGet's MigrationRunner uses Environment.GetFolderPath rather than
        # LOCALAPPDATA on Windows.  With a redirected USERPROFILE, .NET returns
        # an empty LocalApplicationData path until this conventional directory
        # exists, which otherwise makes NuGet create `NuGet/Migrations/1`
        # relative to the build working directory.
        roaming_appdata = profile_root / "AppData" / "Roaming"
        local_appdata = profile_root / "AppData" / "Local"
    else:
        roaming_appdata = environment_root / "appdata" / "roaming"
        local_appdata = environment_root / "appdata" / "local"
    directories = {
        "APPDATA": roaming_appdata,
        "LOCALAPPDATA": local_appdata,
        "HOME": profile_root,
        "USERPROFILE": profile_root,
        "TEMP": environment_root / "temp",
        "TMP": environment_root / "temp",
        "ProgramData": environment_root / "program-data",
        "ProgramFiles": environment_root / "program-files",
        "ProgramFiles(x86)": environment_root / "program-files-x86",
        "CommonProgramFiles": environment_root / "common-program-files",
        "CommonProgramFiles(x86)": environment_root / "common-program-files-x86",
        "NUGET_PACKAGES": environment_root / "nuget" / "packages",
        "NUGET_HTTP_CACHE_PATH": environment_root / "nuget" / "http-cache",
        "NUGET_PLUGINS_CACHE_PATH": environment_root / "nuget" / "plugins-cache",
        "NUGET_SCRATCH": environment_root / "nuget" / "scratch",
    }
    for path in set(directories.values()):
        path.mkdir(parents=True, exist_ok=True)
        _require_safe_ancestors(environment_root, path)
    environment = _sanitized_environment()
    environment.update({key: str(path) for key, path in directories.items()})
    environment.update(
        {
            "DOTNET_CLI_HOME": str(directories["HOME"]),
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_MULTILEVEL_LOOKUP": "0",
            "DOTNET_NOLOGO": "1",
            "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        }
    )
    if dotnet is not None:
        dotnet = dotnet.resolve(strict=True)
        _require_regular_file(dotnet, "isolated dotnet executable")
        search_paths = [str(dotnet.parent)]
        system_root = environment.get("SYSTEMROOT") or environment.get("WINDIR")
        if system_root:
            search_paths.append(str(Path(system_root) / "System32"))
        environment["PATH"] = (";" if os.name == "nt" else ":").join(search_paths)
        environment["DOTNET_ROOT"] = str(dotnet.parent)
    return environment


def _verify_requested_inputs(root: Path, inputs: Sequence[Mapping[str, str]]) -> None:
    for item in inputs:
        if _hash_repository_file(root, item["path"]) != item["sha256"]:
            raise TrustedCollectorError(f"trusted evidence input changed: {item['path']}")


def _hash_repository_file(root: Path, relative: str) -> str:
    relative = _relative_path(relative, "repository input path")
    path = root.joinpath(*PurePosixPath(relative).parts)
    try:
        resolved = path.resolve(strict=True)
        resolved.relative_to(root)
    except (OSError, ValueError) as exception:
        raise TrustedCollectorError(f"repository input escaped the root: {relative}") from exception
    _require_regular_unlinked_file(resolved, f"repository input {relative}")
    return _sha256_file(resolved)


def _find_exact_assembly(
    bin_root: Path,
    assembly_name: str,
    *,
    allow_identical_copies: bool = False,
) -> Path:
    _require_directory(bin_root, "fresh build output directory")
    _require_safe_ancestors(bin_root, bin_root)
    candidates = tuple(
        path
        for path in bin_root.rglob(f"{assembly_name}.dll")
        if "ref" not in {part.casefold() for part in path.parts}
    )
    if not candidates:
        raise TrustedCollectorError(
            f"fresh build must emit exactly one {assembly_name}.dll; found {len(candidates)}"
        )
    for candidate in candidates:
        _require_safe_ancestors(bin_root, candidate.parent)
        _require_regular_unlinked_file(candidate, f"assembly {assembly_name}")
    if len(candidates) > 1:
        hashes = {_sha256_file(candidate) for candidate in candidates}
        if not allow_identical_copies or len(hashes) != 1:
            raise TrustedCollectorError(
                f"fresh build emitted ambiguous {assembly_name}.dll files; found {len(candidates)}"
            )
    return sorted(candidates, key=lambda item: item.as_posix())[0]


def _resolve_code_base(value: str, trx_path: Path, bin_root: Path) -> Path:
    raw = _text(value, "TRX codeBase")
    if any(character in raw for character in "*?[]"):
        raise TrustedCollectorError("TRX codeBase must be a literal path")
    bin_root = bin_root.resolve(strict=True)
    candidate = Path(raw)
    if not candidate.is_absolute():
        # A relative TRX value is interpreted once, exactly beneath the fresh
        # output root.  Never search/glob for a matching basename: doing so
        # would let an imprecise or attacker-selected value bind a different
        # assembly merely because it happened to be the only match.
        relative = _relative_path(
            raw.replace("\\", "/"),
            "relative TRX codeBase",
        )
        candidate = bin_root.joinpath(*PurePosixPath(relative).parts)
    try:
        candidate.absolute().relative_to(bin_root)
    except ValueError as exception:
        raise TrustedCollectorError(
            "TRX codeBase escaped the fresh output root"
        ) from exception
    _require_safe_ancestors(bin_root, candidate.parent)
    _require_regular_unlinked_file(candidate, "TRX codeBase")
    resolved = candidate.resolve(strict=True)
    try:
        resolved.relative_to(bin_root)
    except ValueError as exception:
        raise TrustedCollectorError(
            "TRX codeBase escaped the fresh output root"
        ) from exception
    return resolved


def _session_artifact(
    session: Path,
    path: Path,
    *,
    max_bytes: int = _MAX_CHILD_OUTPUT_BYTES,
) -> Mapping[str, str]:
    _require_safe_ancestors(session, path.parent)
    _require_regular_unlinked_file(path, "session artifact")
    resolved = path.resolve(strict=True)
    try:
        relative = resolved.relative_to(session).as_posix()
    except ValueError as exception:
        raise TrustedCollectorError("trusted artifact escaped the session") from exception
    _require_regular_unlinked_file(resolved, f"session artifact {relative}")
    if resolved.stat().st_size > max_bytes:
        raise TrustedCollectorError(f"session artifact exceeded safety limit: {relative}")
    return {
        "bytes": resolved.stat().st_size,
        "path": relative,
        "sha256": _sha256_file(resolved),
    }


def _captured_session_artifact(
    session: Path,
    path: Path,
    captured: bytes,
    context: str,
) -> Mapping[str, Any]:
    try:
        relative = path.absolute().relative_to(session.resolve(strict=True)).as_posix()
    except (OSError, ValueError) as exception:
        raise TrustedCollectorError(f"{context} escaped the session") from exception
    descriptor: Mapping[str, Any] = {
        "bytes": len(captured),
        "path": _relative_path(relative, f"{context} artifact path"),
        "sha256": _sha256_bytes(captured),
    }
    _validate_session_artifact(session, descriptor, context)
    return descriptor


def _write_captured_session_artifact(
    session: Path,
    path: Path,
    captured: bytes,
    context: str,
) -> Mapping[str, Any]:
    _require_safe_ancestors(session, path.parent)
    _write_exclusive(path, captured)
    return _captured_session_artifact(session, path, captured, context)


def _validate_session_artifact(session: Path, value: Any, context: str) -> None:
    item = _mapping(value, f"{context} artifact")
    _exact_keys(item, {"bytes", "path", "sha256"}, f"{context} artifact")
    path = _artifact_path(session, item, context)
    size = item["bytes"]
    if not isinstance(size, int) or isinstance(size, bool) or size < 0:
        raise TrustedCollectorError(f"{context} artifact byte count is invalid")
    if path.stat().st_size != size:
        raise TrustedCollectorError(f"{context} artifact byte count is stale")
    if _sha256_file(path) != _hash(item["sha256"], f"{context} artifact hash"):
        raise TrustedCollectorError(f"{context} artifact hash is invalid")


def _artifact_path(session: Path, value: Any, context: str) -> Path:
    item = _mapping(value, f"{context} artifact")
    relative = _relative_path(item["path"], f"{context} artifact path")
    path = session.joinpath(*PurePosixPath(relative).parts)
    _require_safe_ancestors(session, path.parent)
    _require_regular_unlinked_file(path, f"{context} artifact")
    try:
        resolved = path.resolve(strict=True)
        resolved.relative_to(session)
    except (OSError, ValueError) as exception:
        raise TrustedCollectorError(f"{context} artifact escaped the session") from exception
    _require_regular_unlinked_file(resolved, f"{context} artifact")
    return resolved


def _executed_assertion_from_child(item: Mapping[str, Any]) -> ExecutedAssertion:
    return ExecutedAssertion(
        item["assertion_id"],
        item["test_path"],
        item["test_symbol"],
        item["test_source_sha256"],
        item["outcome"],
        item["skipped"],
        item["structural_only"],
        item["exercised_load"],
        item["output_sha256"],
    )


def _require_safe_ancestors(anchor: Path, target: Path) -> None:
    anchor = anchor.resolve(strict=True)
    try:
        # Keep the lexical path for the ancestor walk. Resolving ``target``
        # first would erase a junction/symlink segment before it can be
        # rejected.
        relative = target.absolute().relative_to(anchor)
    except ValueError as exception:
        raise TrustedCollectorError("trusted path escaped the repository") from exception
    if any(part in {".", ".."} for part in relative.parts):
        raise TrustedCollectorError("trusted path contains a non-canonical ancestor")
    current = anchor
    _reject_reparse(current, "trusted path anchor")
    for part in relative.parts:
        current /= part
        if current.exists():
            _reject_reparse(current, "trusted path ancestor")


def _require_directory(path: Path, context: str) -> None:
    if not path.is_dir():
        raise TrustedCollectorError(f"{context} must be a directory")
    _reject_reparse(path, context)


def _require_regular_unlinked_file(path: Path, context: str) -> None:
    _require_regular_file(path, context)
    metadata = path.stat(follow_symlinks=False)
    if metadata.st_nlink != 1:
        raise TrustedCollectorError(f"{context} must not be hardlinked")


def _require_regular_file(path: Path, context: str) -> None:
    try:
        metadata = path.stat(follow_symlinks=False)
    except OSError as exception:
        raise TrustedCollectorError(f"cannot inspect {context}: {exception}") from exception
    if not stat.S_ISREG(metadata.st_mode):
        raise TrustedCollectorError(f"{context} must be a regular file")
    _reject_reparse(path, context)


def _reject_reparse(path: Path, context: str) -> None:
    try:
        metadata = path.lstat()
    except OSError as exception:
        raise TrustedCollectorError(f"cannot inspect {context}: {exception}") from exception
    attributes = getattr(metadata, "st_file_attributes", 0)
    reparse = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    if stat.S_ISLNK(metadata.st_mode) or attributes & reparse:
        raise TrustedCollectorError(f"{context} must not be a symlink, junction, or reparse point")


def _write_exclusive(path: Path, value: bytes) -> None:
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(value)
            stream.flush()
            os.fsync(stream.fileno())
    except Exception:
        try:
            path.unlink()
        except OSError:
            pass
        raise


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return f"sha256:{digest.hexdigest()}"


def _sha256_bytes(value: bytes) -> str:
    return f"sha256:{hashlib.sha256(value).hexdigest()}"


def _sha256_data(value: Any) -> str:
    return _sha256_bytes(_canonical_json_bytes(value))


def _hmac_sha256(secret: bytes, value: bytes) -> str:
    return f"sha256:{hmac.new(secret, value, hashlib.sha256).hexdigest()}"


def _canonical_json_bytes(value: Any) -> bytes:
    try:
        return json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    except (TypeError, ValueError) as exception:
        raise TrustedCollectorError("value is not finite deterministic JSON") from exception


def _reject_json_constant(value: str) -> None:
    raise ValueError(f"non-finite JSON constant is forbidden: {value}")


def _reject_duplicate_json_object(pairs: Sequence[tuple[str, Any]]) -> Mapping[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise TrustedCollectorError(f"JSON contains duplicate key '{key}'")
        result[key] = value
    return result


def _json_loads(value: str | bytes) -> Any:
    return json.loads(
        value,
        parse_constant=_reject_json_constant,
        object_pairs_hook=_reject_duplicate_json_object,
    )


def _mapping(value: Any, context: str) -> Mapping[str, Any]:
    if not isinstance(value, dict) or not all(isinstance(key, str) for key in value):
        raise TrustedCollectorError(f"{context} must be an object")
    return value


def _sequence(value: Any, context: str) -> list[Any]:
    if not isinstance(value, list):
        raise TrustedCollectorError(f"{context} must be a list")
    return value


def _exact_keys(value: Mapping[str, Any], keys: set[str], context: str) -> None:
    missing = keys.difference(value)
    unknown = set(value).difference(keys)
    if missing:
        raise TrustedCollectorError(f"{context} is missing key '{sorted(missing)[0]}'")
    if unknown:
        raise TrustedCollectorError(f"{context} contains unknown key '{sorted(unknown)[0]}'")


def _text(value: Any, context: str) -> str:
    if not isinstance(value, str) or not value or value != value.strip():
        raise TrustedCollectorError(f"{context} must be non-empty trimmed text")
    return value


def _hash(value: Any, context: str) -> str:
    text = _text(value, context)
    if _SHA256.fullmatch(text) is None:
        raise TrustedCollectorError(f"{context} must be a lowercase sha256 hash")
    return text


def _identifier(value: Any, context: str) -> str:
    text = _text(value, context)
    if _IDENTIFIER.fullmatch(text) is None:
        raise TrustedCollectorError(f"{context} must be a lowercase hyphenated id")
    return text


def _symbol(value: Any, context: str) -> str:
    text = _text(value, context)
    if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*", text) is None:
        raise TrustedCollectorError(f"{context} must be an exact qualified symbol")
    return text


def _relative_path(value: Any, context: str) -> str:
    text = _text(value, context)
    if (
        "\\" in text
        or text.startswith("/")
        or any(character in text for character in _WINDOWS_INVALID_PATH_CHARACTERS)
        or any(ord(character) < 32 for character in text)
    ):
        raise TrustedCollectorError(f"{context} must be an exact relative POSIX path")
    raw_parts = text.split("/")
    if any(not part for part in raw_parts):
        raise TrustedCollectorError(f"{context} contains an empty path segment")
    path = PurePosixPath(text)
    if not path.parts or any(part in {"", ".", ".."} for part in path.parts):
        raise TrustedCollectorError(f"{context} contains an invalid segment")
    for part in path.parts:
        if part.endswith((" ", ".")):
            raise TrustedCollectorError(
                f"{context} contains a Windows-ambiguous trailing dot or space"
            )
        if part.split(".", 1)[0].casefold() in _WINDOWS_RESERVED_PATH_PARTS:
            raise TrustedCollectorError(
                f"{context} contains a Windows-reserved path segment"
            )
    return path.as_posix()


def _pattern_text(value: Any, pattern: str, context: str) -> str:
    text = _text(value, context)
    if re.fullmatch(pattern, text) is None:
        raise TrustedCollectorError(f"{context} has an invalid format")
    return text


def _normalize_text_sequence(value: Any, context: str, *, pattern: str) -> list[str]:
    result = [_pattern_text(item, pattern, context) for item in _sequence(value, context)]
    if result != sorted(set(result)):
        raise TrustedCollectorError(f"{context} must be unique and sorted")
    return result


def _freeze_authority_function_graph() -> Mapping[str, Callable[..., Any]]:
    """Clone module functions over an import-time dependency snapshot.

    Ordinary module-global monkeypatching must not replace the child launcher,
    artifact validator, subprocess runner, or cryptographic primitives used by
    the authority issuer. Deliberate closure/function-globals introspection is
    arbitrary code inside the documented process trust boundary and is not a
    Python sandbox goal.
    """

    namespace = dict(globals())
    namespace.update(
        {
            "base64": types.SimpleNamespace(
                b64decode=base64.b64decode,
                b64encode=base64.b64encode,
            ),
            "ElementTree": types.SimpleNamespace(
                Element=ElementTree.Element,
                ParseError=ElementTree.ParseError,
                SubElement=ElementTree.SubElement,
                fromstring=ElementTree.fromstring,
                parse=ElementTree.parse,
                tostring=ElementTree.tostring,
            ),
            "hashlib": types.SimpleNamespace(sha256=hashlib.sha256),
            "hmac": types.SimpleNamespace(
                compare_digest=hmac.compare_digest,
                new=hmac.new,
            ),
            "json": types.SimpleNamespace(
                JSONDecodeError=json.JSONDecodeError,
                dumps=json.dumps,
                loads=json.loads,
            ),
            "os": types.SimpleNamespace(
                O_CREAT=os.O_CREAT,
                O_EXCL=os.O_EXCL,
                O_WRONLY=os.O_WRONLY,
                environ=os.environ,
                fdopen=os.fdopen,
                fsync=os.fsync,
                name=os.name,
                open=os.open,
                sep=os.sep,
                walk=os.walk,
            ),
            "secrets": types.SimpleNamespace(
                token_bytes=secrets.token_bytes,
                token_hex=secrets.token_hex,
            ),
            "shutil": types.SimpleNamespace(which=shutil.which),
            "subprocess": types.SimpleNamespace(
                CompletedProcess=subprocess.CompletedProcess,
                PIPE=subprocess.PIPE,
                TimeoutExpired=subprocess.TimeoutExpired,
                run=subprocess.run,
            ),
            "sys": types.SimpleNamespace(executable=str(sys.executable)),
            "uuid": types.SimpleNamespace(uuid4=uuid.uuid4),
            "weakref": types.SimpleNamespace(ref=weakref.ref),
        }
    )
    original_functions = {
        name: value
        for name, value in globals().items()
        if isinstance(value, types.FunctionType) and value.__globals__ is globals()
    }
    frozen: dict[str, Callable[..., Any]] = {}
    for name, function in original_functions.items():
        clone = types.FunctionType(
            function.__code__,
            namespace,
            function.__name__,
            function.__defaults__,
            function.__closure__,
        )
        clone.__kwdefaults__ = function.__kwdefaults__
        clone.__annotations__ = dict(function.__annotations__)
        clone.__dict__.update(function.__dict__)
        clone.__doc__ = function.__doc__
        clone.__module__ = function.__module__
        clone.__qualname__ = function.__qualname__
        frozen[name] = clone
    namespace.update(frozen)
    return frozen


def _install_authority_api() -> tuple[
    Callable[..., Any],
    Callable[[object], bool],
    Callable[[object], str | None],
    Callable[[object], Mapping[str, Any] | None],
]:
    frozen = _freeze_authority_function_graph()
    trace_builder = frozen["_authority_trace_data"]
    collect_and_seal, validate = frozen["_create_authority_boundary"](
        frozen["_collect_unsealed_evidence"]
    )

    def collect(
        repository_root: Path,
        inventory: Any,
        symbol_evidence: SymbolEvidenceRegistry,
        required_assertion_ids: Sequence[str],
        *,
        sessions_root: Path | None = None,
        target_framework: str = "net8.0-windows",
        timeout_seconds: int = 1800,
    ) -> TrustedEvidenceResults:
        required = tuple(required_assertion_ids)
        if not required:
            raise TrustedCollectorError(
                "trusted evidence collection requires at least one assertion"
            )
        return collect_and_seal(
            repository_root,
            inventory,
            symbol_evidence,
            required,
            sessions_root=sessions_root,
            target_framework=target_framework,
            timeout_seconds=timeout_seconds,
        )

    def is_authoritative(value: object) -> bool:
        try:
            return validate(value)
        except (AttributeError, TypeError, ValueError):
            return False

    def receipt_sha256(value: object) -> str | None:
        if not is_authoritative(value):
            return None
        assert isinstance(value, TrustedEvidenceResults)
        return value.authority_receipt_sha256

    def artifact_trace(value: object) -> Mapping[str, Any] | None:
        if not is_authoritative(value):
            return None
        assert isinstance(value, TrustedEvidenceResults)
        return dict(trace_builder(value))

    return collect, is_authoritative, receipt_sha256, artifact_trace


(
    collect_trusted_evidence,
    is_authoritative_evidence_results,
    authority_receipt_sha256,
    authority_artifact_trace,
) = _install_authority_api()


if __name__ == "__main__":
    if sys.argv != [sys.argv[0], "--child"]:
        print("trusted-evidence-child: --child is required", file=sys.stderr)
        raise SystemExit(2)
    raise SystemExit(_child_entrypoint())

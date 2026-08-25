"""Command-line entry point for the GonieGonie upstream tracker."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys
from typing import Sequence

from .atomic_io import write_text_atomically
from .classifier import compare_sources, inspect_source_identity
from .compatibility import (
    CompatibilityConfiguration,
    build_compatibility_report,
    build_public_inventory,
    build_reverification_matrix,
    load_compatibility_configuration,
    load_compatibility_scope,
    load_public_inventory,
    rebase_compatibility_inventory,
    render_compatibility_matrix,
    render_public_inventory,
    write_compatibility_report,
)
from .config import TrackerConfiguration, load_configuration
from .errors import SourceError, TrackerError
from .evidence import (
    load_evidence_results,
    render_scope_decisions,
    render_symbol_evidence,
)
from .reporting import write_reports
from .scope_policy import build_safe_scope_plan
from .symbols import build_snapshot


def main(arguments: Sequence[str] | None = None) -> int:
    """Run the tracker CLI and return a process exit code."""

    try:
        _require_python_312()
        parser = _create_parser()
        options = parser.parse_args(arguments)
        repository_root = options.repository_root.resolve()
        configuration = _configuration(options, repository_root)
        if options.command == "validate":
            compatibility = _compatibility_configuration(options, repository_root, configuration)
            return _validate(configuration, compatibility)
        if options.command == "hash":
            return _hash(configuration, options, repository_root)
        if options.command == "compare":
            return _compare(configuration, options, repository_root)
        if options.command == "inventory":
            return _inventory(configuration, options, repository_root)
        if options.command == "matrix-template":
            return _matrix_template(configuration, options, repository_root)
        if options.command == "rebase-inventory":
            compatibility = _compatibility_configuration(options, repository_root, configuration)
            return _rebase_inventory(compatibility, options, repository_root)
        if options.command == "apply-safe-scope":
            compatibility = _compatibility_configuration(options, repository_root, configuration)
            return _apply_safe_scope(compatibility, options, repository_root)
        if options.command == "compatibility-report":
            compatibility = _compatibility_configuration(options, repository_root, configuration)
            return _compatibility_report(
                compatibility,
                options,
                repository_root,
                fail_on_incomplete=False,
            )
        if options.command == "compatibility-gate":
            compatibility = _compatibility_configuration(options, repository_root, configuration)
            return _compatibility_report(
                compatibility,
                options,
                repository_root,
                fail_on_incomplete=True,
            )
        parser.error("a command is required")
    except TrackerError as exception:
        print(f"upstream-tracker: {exception}", file=sys.stderr)
        return 2
    return 2


def _create_parser() -> argparse.ArgumentParser:
    repository_root = _discover_repository_root()
    parser = argparse.ArgumentParser(
        prog="goniegonie-upstream-tracker",
        description="Classify historical source drift and map impacted GonieGonie tests.",
    )
    parser.add_argument(
        "--repository-root",
        type=Path,
        default=repository_root,
        help="Repository root containing upstream tracking manifests.",
    )
    parser.add_argument("--lock", type=Path, help="Override upstream lock JSON path.")
    parser.add_argument("--port-map", type=Path, help="Override port-map YAML path.")
    parser.add_argument(
        "--compatibility-exceptions",
        type=Path,
        help="Override compatibility-exceptions YAML path.",
    )
    parser.add_argument(
        "--compatibility-scope",
        type=Path,
        help="Override compatibility-scope JSON path.",
    )
    parser.add_argument(
        "--public-symbol-inventory",
        type=Path,
        help="Override public-symbol-inventory JSON path.",
    )
    parser.add_argument(
        "--compatibility-matrix",
        type=Path,
        help="Override compatibility-matrix JSON path.",
    )
    parser.add_argument(
        "--symbol-evidence",
        type=Path,
        help="Override exact symbol-evidence JSON path.",
    )
    parser.add_argument(
        "--scope-decisions",
        type=Path,
        help="Override exact product-scope decisions JSON path.",
    )
    commands = parser.add_subparsers(dest="command", required=True)

    commands.add_parser("validate", help="Validate all manifests and cross-references.")

    hash_parser = commands.add_parser("hash", help="Generate Python AST symbol hashes.")
    hash_parser.add_argument("--source-root", type=Path, required=True)
    hash_parser.add_argument(
        "--output",
        type=Path,
        help="JSON output path; defaults beneath temp/upstream-tracker.",
    )
    hash_parser.add_argument(
        "--allow-missing-tracked-paths",
        action="store_true",
        help="Permit a partial candidate tree.",
    )

    compare_parser = commands.add_parser(
        "compare",
        help="Compare a pinned clone or source export with a current source root.",
    )
    compare_parser.add_argument("--baseline-source", type=Path, required=True)
    compare_parser.add_argument("--current-source", type=Path, required=True)
    compare_parser.add_argument(
        "--output-dir",
        type=Path,
        help="Output directory; defaults beneath temp/upstream-tracker.",
    )
    compare_parser.add_argument(
        "--require-verified-pin",
        action="store_true",
        help="Require the baseline to be a clean Git clone at the locked commit and origin.",
    )
    compare_parser.add_argument(
        "--fail-on-drift",
        action="store_true",
        help="Return exit code 3 when any tracked drift exists.",
    )
    compare_parser.add_argument(
        "--fail-on-unmapped",
        action="store_true",
        help="Return exit code 4 when a changed symbol lacks a port mapping.",
    )

    inventory_parser = commands.add_parser(
        "inventory",
        help="Regenerate the exhaustive public-symbol inventory from the exact pinned clone.",
    )
    inventory_parser.add_argument("--source-root", type=Path, required=True)
    inventory_parser.add_argument(
        "--output",
        type=Path,
        help="JSON output path; defaults beneath temp/upstream-tracker.",
    )

    matrix_parser = commands.add_parser(
        "matrix-template",
        help="Create a fail-closed matrix template from the pinned public inventory.",
    )
    matrix_parser.add_argument(
        "--output",
        type=Path,
        help="JSON output path; defaults beneath temp/upstream-tracker.",
    )

    rebase_parser = commands.add_parser(
        "rebase-inventory",
        help="Rebind registries to byte-only inventory drift with an identical AST contract.",
    )
    rebase_parser.add_argument(
        "--replacement-inventory",
        type=Path,
        required=True,
        help="New generated public-symbol inventory JSON.",
    )
    rebase_parser.add_argument(
        "--output-dir",
        type=Path,
        help="Output directory; defaults beneath temp/upstream-tracker/rebased.",
    )

    scope_parser = commands.add_parser(
        "apply-safe-scope",
        help="Generate the exact reviewed 250-symbol product-scope integration.",
    )
    scope_parser.add_argument(
        "--output-dir",
        type=Path,
        help="Output directory; defaults beneath temp/upstream-tracker/safe-scope.",
    )
    scope_parser.add_argument(
        "--write-canonical",
        action="store_true",
        help="Replace the two canonical scope manifests after exact validation.",
    )

    report_parser = commands.add_parser(
        "compatibility-report",
        help="Write the machine-readable symbol-classification coverage report.",
    )
    report_parser.add_argument("--source-root", type=Path)
    report_parser.add_argument(
        "--require-verified-pin",
        action="store_true",
        help="Require source-root to be the clean locked Git clone and origin.",
    )
    report_parser.add_argument(
        "--output",
        type=Path,
        help="JSON output path; defaults beneath temp/upstream-tracker.",
    )
    report_parser.add_argument(
        "--evidence-results",
        type=Path,
        action="append",
        default=[],
        help="Collected exact assertion-result JSON; repeat for multiple test collectors.",
    )

    gate_parser = commands.add_parser(
        "compatibility-gate",
        help="Fail unless the exact pinned inventory is fully classified and reverified.",
    )
    gate_parser.add_argument("--source-root", type=Path, required=True)
    gate_parser.add_argument(
        "--output",
        type=Path,
        help="JSON output path; defaults beneath temp/upstream-tracker.",
    )
    gate_parser.add_argument(
        "--evidence-results",
        type=Path,
        action="append",
        default=[],
        help="Collected exact assertion-result JSON; repeat for multiple test collectors.",
    )
    return parser


def _configuration(options: argparse.Namespace, repository_root: Path) -> TrackerConfiguration:
    lock = _manifest_path(options.lock, repository_root / "upstream" / "upstream.lock.json")
    port_map = _manifest_path(options.port_map, repository_root / "upstream" / "port-map.yml")
    exceptions = _manifest_path(
        options.compatibility_exceptions,
        repository_root / "upstream" / "compatibility-exceptions.yml",
    )
    return load_configuration(lock, port_map, exceptions)


def _compatibility_configuration(
    options: argparse.Namespace,
    repository_root: Path,
    tracker: TrackerConfiguration,
) -> CompatibilityConfiguration:
    scope = _manifest_path(
        options.compatibility_scope,
        repository_root / "upstream" / "compatibility-scope.json",
    )
    inventory = _manifest_path(
        options.public_symbol_inventory,
        repository_root / "upstream" / "public-symbol-inventory.json",
    )
    matrix = _manifest_path(
        options.compatibility_matrix,
        repository_root / "upstream" / "compatibility-matrix.json",
    )
    evidence = _manifest_path(
        options.symbol_evidence,
        repository_root / "upstream" / "symbol-evidence.json",
    )
    decisions = _manifest_path(
        options.scope_decisions,
        repository_root / "upstream" / "scope-decisions.json",
    )
    return load_compatibility_configuration(
        tracker,
        scope,
        inventory,
        matrix,
        evidence,
        decisions,
        repository_root,
    )


def _validate(
    configuration: TrackerConfiguration,
    compatibility: CompatibilityConfiguration,
) -> int:
    classification_counts = {
        status: sum(
            entry.classification == status
            for entry in compatibility.matrix.entries
        )
        for status in (
            "equivalent",
            "exception",
            "out_of_scope",
            "needs_reverification",
        )
    }
    data = {
        "compatibility_exception_count": len(configuration.exceptions),
        "compatibility_exception_ids": sorted(
            item.identifier for item in configuration.exceptions
        ),
        "compatibility": {
            "classification_counts": classification_counts,
            "complete": not compatibility.needs_reverification,
            "exact_inventory_coverage": True,
            "repository_manifest_bindings": compatibility.exact_registry_coverage,
            "inventory_sha256": compatibility.inventory.content_sha256,
            "matrix_sha256": compatibility.matrix.content_sha256,
            "scope_decisions_sha256": compatibility.scope_decisions.content_sha256,
            "scope_decision_count": len(compatibility.scope_decisions.decisions),
            "symbol_evidence_sha256": compatibility.symbol_evidence.content_sha256,
            "symbol_evidence_entry_count": len(compatibility.symbol_evidence.entries),
            "symbol_evidence_receipt_count": len(compatibility.symbol_evidence.receipts),
            "public_symbol_count": len(compatibility.inventory.symbols),
            "python_file_count": len(compatibility.inventory.files),
        },
        "mapping_count": len(configuration.mappings),
        "module_count": len(configuration.lock.modules),
        "schema": "goniegonie.upstream-validation.v1",
        "tracked_path_count": len(configuration.tracked_paths),
        "valid": True,
    }
    print(json.dumps(data, indent=2, sort_keys=True))
    return 0


def _inventory(
    configuration: TrackerConfiguration,
    options: argparse.Namespace,
    repository_root: Path,
) -> int:
    scope_path = _manifest_path(
        options.compatibility_scope,
        repository_root / "upstream" / "compatibility-scope.json",
    )
    scope = load_compatibility_scope(scope_path, configuration)
    identity = inspect_source_identity(
        options.source_root,
        expected_commit=configuration.lock.commit,
        expected_repository=configuration.lock.repository,
    )
    if not identity.pin_verified:
        raise SourceError(
            "Public inventory generation requires a clean Git clone at the locked commit and origin"
        )
    inventory = build_public_inventory(options.source_root, scope)
    output = options.output or (
        repository_root / "temp" / "upstream-tracker" / "public-symbol-inventory.json"
    )
    output = _require_temp_output(output, repository_root, file_path=True)
    _write_text(output, render_public_inventory(inventory), "public symbol inventory")
    print(output)
    return 0


def _compatibility_report(
    compatibility: CompatibilityConfiguration,
    options: argparse.Namespace,
    repository_root: Path,
    *,
    fail_on_incomplete: bool,
) -> int:
    source_root: Path | None = options.source_root
    if source_root is not None:
        require_verified = fail_on_incomplete or bool(
            getattr(options, "require_verified_pin", False)
        )
        identity = inspect_source_identity(
            source_root,
            expected_commit=(compatibility.tracker.lock.commit if require_verified else None),
            expected_repository=(compatibility.tracker.lock.repository if require_verified else None),
        )
        if require_verified and not identity.pin_verified:
            raise SourceError(
                "Compatibility verification requires a clean Git clone at the locked commit and origin"
            )
    elif bool(getattr(options, "require_verified_pin", False)):
        raise SourceError("--require-verified-pin requires --source-root")

    report = build_compatibility_report(
        compatibility,
        source_root=source_root,
        evidence_results=tuple(
            load_evidence_results(
                path,
                compatibility.inventory,
                compatibility.symbol_evidence,
                repository_root=repository_root,
            )
            for path in getattr(options, "evidence_results", ())
        ),
    )
    default_name = "compatibility-gate.json" if fail_on_incomplete else "compatibility-report.json"
    output = options.output or repository_root / "temp" / "upstream-tracker" / default_name
    output = _require_temp_output(output, repository_root, file_path=True)
    write_compatibility_report(report, output)
    print(
        json.dumps(
            {
                "classification_complete": report.classification_complete,
                "output": str(output),
                "passed": report.passed,
                "required_symbol_evidence_satisfied": report.evidence_execution.passed,
                "public_symbol_count": len(compatibility.inventory.symbols),
                "source_matches_inventory": report.source_matches_inventory,
                "unresolved_count": len(compatibility.needs_reverification),
            },
            indent=2,
            sort_keys=True,
        )
    )
    return 5 if fail_on_incomplete and not report.passed else 0


def _matrix_template(
    configuration: TrackerConfiguration,
    options: argparse.Namespace,
    repository_root: Path,
) -> int:
    scope_path = _manifest_path(
        options.compatibility_scope,
        repository_root / "upstream" / "compatibility-scope.json",
    )
    inventory_path = _manifest_path(
        options.public_symbol_inventory,
        repository_root / "upstream" / "public-symbol-inventory.json",
    )
    scope = load_compatibility_scope(scope_path, configuration)
    inventory = load_public_inventory(inventory_path, scope)
    matrix = build_reverification_matrix(inventory, configuration.exceptions)
    output = options.output or (
        repository_root / "temp" / "upstream-tracker" / "compatibility-matrix-template.json"
    )
    output = _require_temp_output(output, repository_root, file_path=True)
    _write_text(output, render_compatibility_matrix(matrix), "compatibility matrix template")
    print(output)
    return 0


def _rebase_inventory(
    configuration: CompatibilityConfiguration,
    options: argparse.Namespace,
    repository_root: Path,
) -> int:
    replacement = load_public_inventory(
        options.replacement_inventory.resolve(),
        configuration.scope,
    )
    rebased = rebase_compatibility_inventory(configuration, replacement)
    output = options.output_dir or repository_root / "temp" / "upstream-tracker" / "rebased"
    output = _require_temp_output(output, repository_root, file_path=False)
    paths = {
        "public_symbol_inventory": output / "public-symbol-inventory.json",
        "compatibility_matrix": output / "compatibility-matrix.json",
        "symbol_evidence": output / "symbol-evidence.json",
        "scope_decisions": output / "scope-decisions.json",
    }
    _write_text(
        paths["public_symbol_inventory"],
        render_public_inventory(rebased.inventory),
        "rebased public symbol inventory",
    )
    _write_text(
        paths["compatibility_matrix"],
        render_compatibility_matrix(rebased.matrix),
        "rebased compatibility matrix",
    )
    _write_text(
        paths["symbol_evidence"],
        render_symbol_evidence(rebased.symbol_evidence),
        "rebased symbol evidence",
    )
    _write_text(
        paths["scope_decisions"],
        render_scope_decisions(rebased.scope_decisions),
        "rebased scope decisions",
    )
    print(
        json.dumps(
            {
                "inventory_sha256": rebased.inventory.content_sha256,
                "matrix_sha256": rebased.matrix.content_sha256,
                "outputs": {key: str(value) for key, value in paths.items()},
                "schema": "goniegonie.upstream-inventory-rebase.v1",
                "symbol_contract_unchanged": True,
            },
            indent=2,
            sort_keys=True,
        )
    )
    return 0


def _apply_safe_scope(
    configuration: CompatibilityConfiguration,
    options: argparse.Namespace,
    repository_root: Path,
) -> int:
    if configuration.scope_decisions is None:
        raise SourceError("Exact scope decisions are required for safe-scope integration")
    plan = build_safe_scope_plan(
        configuration.inventory,
        configuration.matrix,
        configuration.scope_decisions,
    )
    output = options.output_dir or (
        repository_root / "temp" / "upstream-tracker" / "safe-scope"
    )
    output = _require_temp_output(output, repository_root, file_path=False)
    rendered_decisions = render_scope_decisions(plan.decisions)
    rendered_matrix = render_compatibility_matrix(plan.matrix)
    outputs = {
        "scope_decisions": output / "scope-decisions.json",
        "compatibility_matrix": output / "compatibility-matrix.json",
    }
    _write_text(outputs["scope_decisions"], rendered_decisions, "safe scope decisions")
    _write_text(outputs["compatibility_matrix"], rendered_matrix, "safe scope matrix")

    canonical_written = False
    if options.write_canonical:
        manifest_overrides = any(
            getattr(options, name) is not None
            for name in (
                "lock",
                "port_map",
                "compatibility_exceptions",
                "compatibility_scope",
                "public_symbol_inventory",
                "compatibility_matrix",
                "symbol_evidence",
                "scope_decisions",
            )
        )
        if manifest_overrides:
            raise SourceError("Canonical safe-scope writes do not accept manifest overrides")
        if not configuration.exact_registry_coverage:
            raise SourceError(
                "Canonical safe-scope writes require clean, exact HEAD compatibility manifests"
            )
        canonical_decisions = repository_root / "upstream" / "scope-decisions.json"
        canonical_matrix = repository_root / "upstream" / "compatibility-matrix.json"
        _write_text(canonical_decisions, rendered_decisions, "canonical scope decisions")
        _write_text(canonical_matrix, rendered_matrix, "canonical compatibility matrix")
        canonical_written = True

    print(
        json.dumps(
            {
                "canonical_written": canonical_written,
                "classification_counts": plan.classification_counts,
                "decision_count": len(plan.decisions.decisions),
                "matrix_sha256": plan.matrix.content_sha256,
                "new_decision_count": plan.new_decision_count,
                "outputs": {key: str(value) for key, value in outputs.items()},
                "previous_decision_count": plan.previous_decision_count,
                "schema": "goniegonie.reviewed-safe-scope-integration.v1",
                "scope_decisions_sha256": plan.decisions.content_sha256,
                "selection_sha256": plan.selection_sha256,
                "symbol_contract_sha256": plan.symbol_contract_sha256,
            },
            indent=2,
            sort_keys=True,
        )
    )
    return 0


def _hash(
    configuration: TrackerConfiguration,
    options: argparse.Namespace,
    repository_root: Path,
) -> int:
    output = options.output or repository_root / "temp" / "upstream-tracker" / "symbol-hashes.json"
    output = _require_temp_output(output, repository_root, file_path=True)
    snapshot = build_snapshot(
        options.source_root,
        configuration.tracked_paths,
        require_tracked_paths=not options.allow_missing_tracked_paths,
    )
    content = json.dumps(snapshot.to_data(), ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    try:
        write_text_atomically(output, content)
    except OSError as exception:
        raise SourceError(f"Cannot write symbol hashes '{output}': {exception}") from exception
    print(output)
    return 0


def _compare(
    configuration: TrackerConfiguration,
    options: argparse.Namespace,
    repository_root: Path,
) -> int:
    output = options.output_dir or repository_root / "temp" / "upstream-tracker" / "report"
    output = _require_temp_output(output, repository_root, file_path=False)
    report = compare_sources(configuration, options.baseline_source, options.current_source)
    if options.require_verified_pin and not report.baseline.pin_verified:
        raise SourceError("Baseline source is an export; a verified pinned Git clone is required")
    template = (
        Path(__file__).resolve().parent.parent / "templates" / "sync-branch.md"
    )
    paths = write_reports(report, output, repository_root, template)
    print(
        json.dumps(
            {
                "change_count": len(report.changes),
                "outputs": [str(path) for path in paths],
                "review_required": report.review_required,
                "unmapped_change_count": report.unmapped_change_count,
            },
            indent=2,
            sort_keys=True,
        )
    )
    if options.fail_on_unmapped and report.unmapped_change_count:
        return 4
    if options.fail_on_drift and report.has_drift:
        return 3
    return 0


def _manifest_path(override: Path | None, default: Path) -> Path:
    return (override or default).resolve()


def _require_temp_output(path: Path, repository_root: Path, *, file_path: bool) -> Path:
    resolved = path.resolve()
    temp_root = (repository_root.resolve() / "temp").resolve()
    candidate = resolved.parent if file_path else resolved
    try:
        candidate.relative_to(temp_root)
    except ValueError as exception:
        raise SourceError(f"Generated output must remain beneath '{temp_root}'") from exception
    return resolved


def _write_text(path: Path, content: str, description: str) -> None:
    try:
        write_text_atomically(path, content)
    except OSError as exception:
        raise SourceError(f"Cannot write {description} '{path}': {exception}") from exception


def _discover_repository_root() -> Path:
    candidates = [Path.cwd(), Path(__file__).resolve().parents[3]]
    for candidate in candidates:
        current = candidate.resolve()
        while current != current.parent:
            if (current / "upstream" / "upstream.lock.json").is_file():
                return current
            current = current.parent
    return Path.cwd().resolve()


def _require_python_312() -> None:
    if sys.version_info[:2] != (3, 12):
        raise SourceError(
            f"Python 3.12 is required; running {sys.version_info.major}.{sys.version_info.minor}"
        )

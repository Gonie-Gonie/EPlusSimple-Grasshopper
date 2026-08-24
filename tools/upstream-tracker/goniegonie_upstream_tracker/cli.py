"""Command-line entry point for the GonieGonie upstream tracker."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys
from typing import Sequence

from .classifier import compare_sources
from .config import TrackerConfiguration, load_configuration
from .errors import SourceError, TrackerError
from .reporting import write_reports
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
            return _validate(configuration)
        if options.command == "hash":
            return _hash(configuration, options, repository_root)
        if options.command == "compare":
            return _compare(configuration, options, repository_root)
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
    return parser


def _configuration(options: argparse.Namespace, repository_root: Path) -> TrackerConfiguration:
    lock = _manifest_path(options.lock, repository_root / "upstream" / "upstream.lock.json")
    port_map = _manifest_path(options.port_map, repository_root / "upstream" / "port-map.yml")
    exceptions = _manifest_path(
        options.compatibility_exceptions,
        repository_root / "upstream" / "compatibility-exceptions.yml",
    )
    return load_configuration(lock, port_map, exceptions)


def _validate(configuration: TrackerConfiguration) -> int:
    data = {
        "compatibility_exception_count": len(configuration.exceptions),
        "mapping_count": len(configuration.mappings),
        "module_count": len(configuration.lock.modules),
        "schema": "goniegonie.upstream-validation.v1",
        "tracked_path_count": len(configuration.tracked_paths),
        "valid": True,
    }
    print(json.dumps(data, indent=2, sort_keys=True))
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
    output.parent.mkdir(parents=True, exist_ok=True)
    content = json.dumps(snapshot.to_data(), ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    temporary = output.with_suffix(output.suffix + ".tmp")
    try:
        temporary.write_text(content, encoding="utf-8", newline="\n")
        temporary.replace(output)
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

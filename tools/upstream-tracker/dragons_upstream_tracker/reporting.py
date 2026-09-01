"""Deterministic JSON, Markdown, and sync-branch report rendering."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any

from .atomic_io import write_text_atomically
from .classifier import ComparisonReport
from .errors import SourceError


def render_json(report: ComparisonReport) -> str:
    """Render canonical, stable JSON with a trailing newline."""

    return json.dumps(
        report.to_data(),
        ensure_ascii=False,
        indent=2,
        sort_keys=True,
    ) + "\n"


def render_markdown(report: ComparisonReport) -> str:
    """Render a compact deterministic human review report."""

    data = report.to_data()
    baseline_commit = report.baseline.commit or report.configuration.lock.commit
    current_commit = report.current.commit or "source-root"
    lines = [
        "# Dragons upstream drift report",
        "",
        f"- Pinned commit: `{baseline_commit}`",
        f"- Current commit: `{current_commit}`",
        f"- Drift detected: `{'yes' if report.has_drift else 'no'}`",
        f"- Review required: `{'yes' if report.review_required else 'no'}`",
        f"- Unmapped changes: `{report.unmapped_change_count}`",
        "",
        "## Classification summary",
        "",
        "| Classification | Count |",
        "|---|---:|",
    ]
    counts = data["summary"]["classification_counts"]
    for classification in sorted(counts):
        lines.append(f"| `{classification}` | {counts[classification]} |")

    lines.extend(["", "## Changed symbols and data", ""])
    if not report.changes:
        lines.append("No tracked changes detected.")
    else:
        current_path: str | None = None
        for change in report.changes:
            if change.path != current_path:
                if current_path is not None:
                    lines.append("")
                lines.append(f"### `{change.path}`")
                lines.append("")
                current_path = change.path
            lines.append(
                f"- `{change.symbol}` — **{change.classification.value}** ({change.symbol_kind})"
            )
            if change.mappings:
                for mapping in change.mappings:
                    tests = ", ".join(f"`{test}`" for test in mapping.tests)
                    lines.append(
                        f"  - {mapping.match} map: `{mapping.project}` / `{mapping.file}` / "
                        f"`{mapping.symbol}`; tests: {tests}"
                    )
            else:
                lines.append("  - No port mapping matched this change.")
            if change.compatibility_exceptions:
                exceptions = ", ".join(
                    f"`{identifier}`" for identifier in change.compatibility_exceptions
                )
                lines.append(f"  - Compatibility exceptions to review: {exceptions}")

    lines.extend(["", "## Impacted Dragons targets", ""])
    _append_list(lines, "Projects", report.impacted.projects)
    _append_list(lines, "Files", report.impacted.files)
    _append_list(lines, "Symbols", report.impacted.symbols)
    _append_list(lines, "Tests", report.impacted.tests)

    lines.extend(["", "## Source verification", ""])
    lines.append(
        f"- Baseline: `{report.baseline.kind}`, pin verified: "
        f"`{'yes' if report.baseline.pin_verified else 'no'}`"
    )
    lines.append(
        f"- Current: `{report.current.kind}`, clean: `{_display_optional_bool(report.current.clean)}`"
    )
    return "\n".join(lines).rstrip() + "\n"


def render_sync_branch(
    report: ComparisonReport,
    template_text: str,
) -> str:
    """Fill the reviewed sync branch checklist template."""

    current_token = report.current.commit
    if current_token is None:
        current_token = hashlib.sha256(render_json(report).encode("utf-8")).hexdigest()
    short_commit = current_token[:7].lower()
    branch_name = f"sync/invisibledragon-simpledragon-{short_commit}"
    changes = (
        "\n".join(
            f"- [ ] `{change.path}` — `{change.symbol}` ({change.classification.value})"
            for change in report.changes
        )
        if report.changes
        else "- [x] No tracked changes detected."
    )
    tests = (
        "\n".join(f"- [ ] `{test}`" for test in report.impacted.tests)
        if report.impacted.tests
        else "- [ ] Add or identify tests for any accepted unmapped change."
    )
    replacements = {
        "{{baseline_commit}}": report.configuration.lock.commit,
        "{{branch_name}}": branch_name,
        "{{change_checklist}}": changes,
        "{{current_commit}}": report.current.commit or "source-root",
        "{{short_commit}}": short_commit,
        "{{test_checklist}}": tests,
    }
    rendered = template_text
    for marker, value in replacements.items():
        rendered = rendered.replace(marker, value)
    unresolved = sorted(
        token.split("}}", 1)[0] + "}}"
        for token in rendered.split("{{")[1:]
        if "}}" in token
    )
    if unresolved:
        raise SourceError(f"Sync template contains unresolved marker '{unresolved[0]}'")
    return rendered.rstrip() + "\n"


def write_reports(
    report: ComparisonReport,
    output_directory: Path,
    repository_root: Path,
    template_path: Path,
) -> tuple[Path, Path, Path]:
    """Write all generated files beneath the repository's disposable temp tree."""

    repository = repository_root.resolve()
    temp_root = (repository / "temp").resolve()
    output = output_directory.resolve()
    try:
        output.relative_to(temp_root)
    except ValueError as exception:
        raise SourceError(f"Report output must remain beneath '{temp_root}'") from exception
    try:
        template = template_path.read_text(encoding="utf-8")
    except OSError as exception:
        raise SourceError(f"Cannot read sync template '{template_path}': {exception}") from exception
    output.mkdir(parents=True, exist_ok=True)
    json_path = output / "report.json"
    markdown_path = output / "report.md"
    sync_path = output / "sync-branch.md"
    targets = (
        (json_path, render_json(report)),
        (markdown_path, render_markdown(report)),
        (sync_path, render_sync_branch(report, template)),
    )
    for path, content in targets:
        _write_atomic(path, content)
    return json_path, markdown_path, sync_path


def _append_list(lines: list[str], label: str, values: tuple[str, ...]) -> None:
    lines.append(f"### {label}")
    lines.append("")
    if values:
        lines.extend(f"- `{value}`" for value in values)
    else:
        lines.append("- None")
    lines.append("")


def _display_optional_bool(value: bool | None) -> str:
    if value is None:
        return "unknown"
    return "yes" if value else "no"


def _write_atomic(path: Path, content: str) -> None:
    try:
        write_text_atomically(path, content)
    except OSError as exception:
        raise SourceError(f"Cannot write report '{path}': {exception}") from exception

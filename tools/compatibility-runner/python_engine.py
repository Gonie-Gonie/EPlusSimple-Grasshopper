"""Run pinned Python 0.7.0 compatibility cases with explicit shared inputs."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
from pathlib import Path
from typing import Any


ADDRESS_PATTERN = re.compile(r"0x[0-9a-fA-F]{7,16}")
DIAGNOSTIC_PATTERN = re.compile(
    r"^\s*\*\*\s*(Warning|Severe|Fatal)\s*\*\*\s*(.*)$",
    re.IGNORECASE | re.MULTILINE,
)
SUMMARY_PATTERN = re.compile(
    r"EnergyPlus (?:Completed Successfully|Terminated).*?"
    r"(?P<warning>\d+) Warning(?:s)?;\s*"
    r"(?P<severe>\d+) Severe Error(?:s)?",
    re.IGNORECASE | re.DOTALL,
)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--upstream-root", type=Path, required=True)
    parser.add_argument("--runtime-root", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--case")
    parser.add_argument("--skip-energyplus", action="store_true")
    return parser.parse_args()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def write_json(path: Path, value: Any) -> None:
    write_text(
        path,
        json.dumps(value, ensure_ascii=False, allow_nan=False, indent=2) + "\n",
    )


def canonicalize_addresses(text: str) -> str:
    replacements: dict[str, str] = {}

    def replace(match: re.Match[str]) -> str:
        source = match.group(0)
        if source not in replacements:
            replacements[source] = f"0xAUTO{len(replacements):04d}"
        return replacements[source]

    return ADDRESS_PATTERN.sub(replace, text)


def git_commit(root: Path) -> str:
    result = subprocess.run(
        ["git", "-C", str(root), "rev-parse", "HEAD"],
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return result.stdout.strip()


def expand_idf(source: Path, destination: Path, runtime_root: Path) -> None:
    work = destination.parent / "python-expand-work"
    if work.exists():
        shutil.rmtree(work)
    work.mkdir(parents=True)
    shutil.copy2(source, work / "in.idf")
    shutil.copy2(runtime_root / "Energy+.idd", work / "Energy+.idd")
    result = subprocess.run(
        [str(runtime_root / "ExpandObjects.exe")],
        cwd=work,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if result.returncode != 0:
        raise RuntimeError(
            f"ExpandObjects failed with {result.returncode}: {result.stderr}"
        )
    expanded = work / "expanded.idf"
    selected = expanded if expanded.exists() else work / "in.idf"
    text = canonicalize_addresses(selected.read_text(encoding="utf-8", errors="replace"))
    if not text.endswith("\n"):
        text += "\n"
    write_text(destination, text)
    shutil.rmtree(work)


def warning_report(error_path: Path) -> dict[str, Any]:
    text = error_path.read_text(encoding="utf-8", errors="replace")
    items = [
        {"severity": match.group(1).title(), "title": match.group(2).strip()}
        for match in DIAGNOSTIC_PATTERN.finditer(text)
    ]
    summary_match = SUMMARY_PATTERN.search(text)
    summary = {
        "warning": int(summary_match.group("warning")) if summary_match else sum(
            item["severity"] == "Warning" for item in items
        ),
        "severe": int(summary_match.group("severe")) if summary_match else sum(
            item["severity"] == "Severe" for item in items
        ),
        "fatal": sum(item["severity"] == "Fatal" for item in items),
    }
    return {
        "schema": "goniegonie.dragons.energyplus-warnings.v1",
        "summary": summary,
        "items": items,
    }


def run_case(
    case: dict[str, Any],
    repository_root: Path,
    runtime_root: Path,
    output_root: Path,
    upstream_commit: str,
    skip_energyplus: bool,
) -> None:
    from epsimple.core import GreenRetrofitModel, GreenRetrofitResult
    from idragon.launcher import run_single

    case_id = str(case["id"])
    case_root = output_root / case_id
    case_root.mkdir(parents=True, exist_ok=True)
    input_path = (repository_root / str(case["input_grm"])).resolve()
    weather_path = (runtime_root / str(case["weather"])).resolve()
    if not input_path.is_file():
        raise FileNotFoundError(input_path)
    if not weather_path.is_file():
        raise FileNotFoundError(weather_path)
    input_sha256 = sha256_file(input_path)
    weather_sha256 = sha256_file(weather_path)
    expected_input_sha256 = str(case["input_grm_sha256"])
    expected_weather_sha256 = str(case["weather_sha256"])
    if input_sha256.casefold() != expected_input_sha256.casefold():
        raise RuntimeError(
            f"Pinned {case_id} GRM hash mismatch: expected "
            f"{expected_input_sha256}, found {input_sha256}."
        )
    if weather_sha256.casefold() != expected_weather_sha256.casefold():
        raise RuntimeError(
            f"Pinned {case_id} weather hash mismatch: expected "
            f"{expected_weather_sha256}, found {weather_sha256}."
        )

    model = GreenRetrofitModel.from_grjson(str(input_path))
    idf = model.to_idf()
    raw_idf = str(idf)
    authoring = case_root / "authoring.idf"
    normalized_idf = canonicalize_addresses(raw_idf)
    write_text(authoring, normalized_idf + ("" if normalized_idf.endswith("\n") else "\n"))

    run_idf = case_root / "python-run.idf"
    write_text(run_idf, raw_idf + ("" if raw_idf.endswith("\n") else "\n"))
    expanded = case_root / "expanded.idf"
    expand_idf(run_idf, expanded, runtime_root)

    produced = [authoring, expanded]
    if not skip_energyplus:
        raw_output = case_root / "energyplus-output"
        raw_output.mkdir(parents=True, exist_ok=True)
        result = run_single(
            str(run_idf),
            str(weather_path),
            ep_dir=str(runtime_root),
            verbose=False,
            output_dir=str(raw_output),
            delete=False,
        )
        grr = GreenRetrofitResult(model, result)
        grr_path = case_root / "result.grr"
        grr.write(str(grr_path))
        error_paths = sorted(raw_output.glob("*.err"))
        if len(error_paths) != 1:
            raise RuntimeError(
                f"Expected one Python EnergyPlus error log; found {len(error_paths)}."
            )
        warnings_path = case_root / "warnings.json"
        write_json(warnings_path, warning_report(error_paths[0]))
        produced.extend([grr_path, warnings_path])

    metadata = {
        "schema": "goniegonie.dragons.compatibility-engine-output.v1",
        "producer": "python-0.7.0",
        "case_id": case_id,
        "upstream_commit": upstream_commit,
        "inputs": {
            "grm": {"path": str(case["input_grm"]), "sha256": input_sha256},
            "weather": {"path": str(case["weather"]), "sha256": weather_sha256},
        },
        "runtime": {
            "energyplus_exe_sha256": sha256_file(runtime_root / "energyplus.exe"),
            "idd_sha256": sha256_file(runtime_root / "Energy+.idd"),
            "expandobjects_sha256": sha256_file(runtime_root / "ExpandObjects.exe"),
        },
        "outputs": [
            {
                "path": path.relative_to(case_root).as_posix(),
                "bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            }
            for path in produced
        ],
    }
    write_json(case_root / "metadata.json", metadata)
    run_idf.unlink(missing_ok=True)


def main() -> None:
    args = parse_arguments()
    repository_root = args.repository_root.resolve()
    upstream_root = args.upstream_root.resolve()
    runtime_root = args.runtime_root.resolve()
    output_root = args.output.resolve()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    expected_commit = str(manifest["upstream_commit"])
    actual_commit = git_commit(upstream_root)
    if actual_commit.lower() != expected_commit.lower():
        raise SystemExit(
            f"Pinned upstream mismatch: expected {expected_commit}, found {actual_commit}."
        )
    output_root.mkdir(parents=True, exist_ok=True)
    selected = [
        case for case in manifest["cases"]
        if args.case is None or str(case["id"]) == args.case
    ]
    if not selected:
        raise SystemExit(f"Unknown compatibility case: {args.case}")
    for case in selected:
        run_case(
            case,
            repository_root,
            runtime_root,
            output_root,
            actual_commit,
            args.skip_energyplus,
        )
    print(f"Python compatibility engine emitted {len(selected)} case(s) to {output_root}")


if __name__ == "__main__":
    main()

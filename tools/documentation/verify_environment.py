"""Verify the repository-local PDF documentation environment exactly."""

from __future__ import annotations

import argparse
import base64
import csv
import hashlib
from importlib import import_module, metadata
import hmac
import io
import json
from pathlib import Path
import re
import struct
import sys


REQUIREMENT_RE = re.compile(
    r"^(?P<name>[A-Za-z0-9_.-]+)==(?P<version>[^\s]+)\s+"
    r"--hash=sha256:(?P<hash>[0-9a-f]{64})$"
)
IMPORT_NAMES = {
    "charset-normalizer": "charset_normalizer",
    "lxml": "lxml",
    "oodocs": "oodocs",
    "pillow": "PIL",
    "pip": "pip",
    "pygments": "pygments",
    "pypdf": "pypdf",
    "python-docx": "docx",
    "reportlab": "reportlab",
    "typing-extensions": "typing_extensions",
}


def canonical_name(value: str) -> str:
    """Return the normalized distribution name used by Python packaging."""

    return re.sub(r"[-_.]+", "-", value).lower()


def read_lock(path: Path) -> dict[str, str]:
    """Read exact package versions and reject any non-canonical lock line."""

    expected: dict[str, str] = {}
    for line_number, raw_line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        match = REQUIREMENT_RE.fullmatch(line)
        if match is None:
            raise RuntimeError(f"Invalid locked requirement at {path}:{line_number}: {line}")
        name = canonical_name(match.group("name"))
        if name in expected:
            raise RuntimeError(f"Duplicate locked distribution: {name}")
        expected[name] = match.group("version")
    if not expected:
        raise RuntimeError(f"The documentation dependency lock is empty: {path}")
    return expected


def installed_distributions() -> dict[str, str]:
    """Return the complete distribution set visible inside this interpreter."""

    installed: dict[str, str] = {}
    for distribution in metadata.distributions():
        name = distribution.metadata.get("Name")
        if not name:
            raise RuntimeError("An installed distribution has no Name metadata.")
        normalized = canonical_name(name)
        if normalized in installed:
            raise RuntimeError(f"Duplicate installed distribution metadata: {normalized}")
        installed[normalized] = distribution.version
    return installed


def assert_venv(expected_python: str) -> None:
    """Require the exact isolated CPython runtime selected by repository setup."""

    actual = ".".join(str(part) for part in sys.version_info[:3])
    if actual != expected_python:
        raise RuntimeError(f"Expected Python {expected_python}; found {actual}.")
    if sys.implementation.name != "cpython" or struct.calcsize("P") != 8:
        raise RuntimeError("Documentation requires 64-bit CPython.")
    if Path(sys.prefix).resolve() == Path(sys.base_prefix).resolve():
        raise RuntimeError("The documentation interpreter is not running inside a venv.")

    configuration = Path(sys.prefix) / "pyvenv.cfg"
    if not configuration.is_file():
        raise RuntimeError(f"The venv configuration is missing: {configuration}")
    config_text = configuration.read_text(encoding="utf-8").lower()
    if not re.search(r"^include-system-site-packages\s*=\s*false\s*$", config_text, re.MULTILINE):
        raise RuntimeError("The documentation venv must exclude system site packages.")


def assert_record_integrity() -> int:
    """Verify every hashed wheel file recorded inside the isolated venv."""

    environment_root = Path(sys.prefix).resolve()
    checked_files = 0
    for distribution in metadata.distributions():
        distribution_name = distribution.metadata.get("Name") or "<unnamed>"
        record_text = distribution.read_text("RECORD")
        if record_text is None:
            raise RuntimeError(f"Installed distribution has no RECORD: {distribution_name}")
        distribution_checked = 0
        for row in csv.reader(io.StringIO(record_text)):
            if len(row) != 3:
                raise RuntimeError(f"Malformed RECORD row in {distribution_name}: {row!r}")
            relative_path, encoded_hash, recorded_size = row
            if not encoded_hash:
                continue
            algorithm, separator, expected_digest_text = encoded_hash.partition("=")
            if separator != "=" or algorithm != "sha256" or not expected_digest_text:
                raise RuntimeError(
                    f"Unsupported RECORD hash in {distribution_name}: {encoded_hash!r}"
                )
            installed_path = Path(distribution.locate_file(relative_path)).resolve()
            if not installed_path.is_relative_to(environment_root):
                raise RuntimeError(
                    f"RECORD path escapes the documentation venv: {installed_path}"
                )
            if not installed_path.is_file():
                raise RuntimeError(
                    f"Hashed installed file is missing from {distribution_name}: {installed_path}"
                )
            if recorded_size and installed_path.stat().st_size != int(recorded_size):
                raise RuntimeError(
                    f"Installed file size differs from RECORD: {installed_path}"
                )
            with installed_path.open("rb") as stream:
                actual_digest = hashlib.file_digest(stream, "sha256").digest()
            padding = "=" * (-len(expected_digest_text) % 4)
            expected_digest = base64.urlsafe_b64decode(expected_digest_text + padding)
            if not hmac.compare_digest(actual_digest, expected_digest):
                raise RuntimeError(
                    f"Installed file hash differs from RECORD: {installed_path}"
                )
            distribution_checked += 1
            checked_files += 1
        if distribution_checked == 0:
            raise RuntimeError(
                f"Installed distribution has no hashed RECORD entries: {distribution_name}"
            )
    return checked_files


def assert_packages(expected: dict[str, str], expected_oodocs: str) -> int:
    """Require the lock to equal the complete visible distribution closure."""

    installed = installed_distributions()
    if installed != expected:
        missing = sorted(set(expected) - set(installed))
        extra = sorted(set(installed) - set(expected))
        changed = sorted(
            name
            for name in set(expected) & set(installed)
            if expected[name] != installed[name]
        )
        raise RuntimeError(
            "Documentation dependency closure differs from its lock; "
            f"missing={missing}, extra={extra}, changed={changed}."
        )

    if expected.get("oodocs") != expected_oodocs:
        raise RuntimeError(
            f"The lock must pin oodocs {expected_oodocs}; found {expected.get('oodocs')}."
        )
    checked_files = assert_record_integrity()
    for distribution_name in sorted(expected):
        module_name = IMPORT_NAMES.get(distribution_name)
        if module_name is None:
            raise RuntimeError(f"No import verification is defined for {distribution_name}.")
        import_module(module_name)

    oodocs = import_module("oodocs")
    if getattr(oodocs, "__version__", None) != expected_oodocs:
        raise RuntimeError(
            f"Expected oodocs {expected_oodocs}; imported {getattr(oodocs, '__version__', None)}."
        )
    return checked_files


def render_smoke_pdf(path: Path) -> None:
    """Render and inspect one minimal PDF through OODocs itself."""

    from oodocs import Document, DocumentMetadata, DocumentSettings, Paragraph

    path.parent.mkdir(parents=True, exist_ok=True)
    document = Document(
        "Dragon documentation environment",
        Paragraph("OODocs PDF rendering is ready."),
        settings=DocumentSettings(
            metadata=DocumentMetadata(
                author="Gonie-Gonie",
                description="Repository setup smoke document",
            )
        ),
    )
    rendered = Path(document.save(path))
    if rendered.resolve() != path.resolve():
        raise RuntimeError(f"OODocs wrote an unexpected smoke path: {rendered}")
    payload = path.read_bytes()
    if len(payload) < 1_000 or not payload.startswith(b"%PDF-"):
        raise RuntimeError("OODocs did not create a valid non-empty PDF smoke artifact.")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--requirements", required=True, type=Path)
    parser.add_argument("--expected-python", required=True)
    parser.add_argument("--expected-oodocs", required=True)
    parser.add_argument("--smoke-output", type=Path)
    args = parser.parse_args()

    assert_venv(args.expected_python)
    expected = read_lock(args.requirements)
    checked_files = assert_packages(expected, args.expected_oodocs)
    if args.smoke_output is not None:
        render_smoke_pdf(args.smoke_output)

    print(
        json.dumps(
            {
                "python": args.expected_python,
                "oodocs": args.expected_oodocs,
                "packages": len(expected),
                "record_files": checked_files,
                "venv": str(Path(sys.prefix).resolve()),
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

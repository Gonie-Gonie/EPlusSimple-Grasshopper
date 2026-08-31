"""Validate the repository documentation boundary and publishing worksheet."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote


PUBLIC_FILES = (
    "README.md",
    "installation.md",
    "choosing-a-dragon.md",
    "grasshopper-workflow.md",
    "energyplus-and-weather.md",
    "troubleshooting.md",
    "user-guide/01-workflow.md",
    "user-guide/02-in-out-reference.md",
    "user-guide/03-compatibility.md",
    "user-guide/04-release-notes.md",
)
DEVELOPMENT_FILES = (
    "README.md",
    "compatibility-policy.md",
    "documentation-build.md",
    "example-maintenance.md",
    "release-checklist.md",
    "publishing/food4rhino.md",
    "publishing/weather-rights-review.md",
)
OBSOLETE_PATHS = (
    "docs/installation.md",
    "docs/choosing-a-dragon.md",
    "docs/grasshopper-workflow.md",
    "docs/energyplus-and-weather.md",
    "docs/compatibility.md",
    "docs/troubleshooting.md",
    "docs/release-checklist.md",
    "docs/user-guide",
)
LINK_PATTERN = re.compile(r"!?\[[^\]]*\]\((?P<target><[^>]+>|[^)\s]+)")
EMAIL_PATTERN = re.compile(r"^[^\s@]+@[^\s@]+\.[^\s@]+$")


class DocumentationError(RuntimeError):
    """Raised when documentation cannot be treated as a coherent source set."""


def _read(path: Path) -> str:
    if not path.is_file():
        raise DocumentationError(f"Required documentation file is missing: {path}")
    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError as exc:
        raise DocumentationError(f"Documentation is not valid UTF-8: {path}") from exc
    if not text.strip():
        raise DocumentationError(f"Documentation file is blank: {path}")
    return text


def _validate_hierarchy(repo_root: Path) -> int:
    docs_root = repo_root / "docs"
    direct_markdown = sorted(path.name for path in docs_root.glob("*.md"))
    if direct_markdown != ["README.md"]:
        raise DocumentationError(
            "docs must contain only its audience router at the top level; "
            f"found {direct_markdown}"
        )

    for relative in OBSOLETE_PATHS:
        if (repo_root / relative).exists():
            raise DocumentationError(f"Obsolete mixed-audience documentation remains: {relative}")

    expected = tuple(f"docs/user/{item}" for item in PUBLIC_FILES) + tuple(
        f"docs/development/{item}" for item in DEVELOPMENT_FILES
    )
    for relative in expected:
        _read(repo_root / relative)

    public_surfaces = [repo_root / f"docs/user/{item}" for item in PUBLIC_FILES]
    public_surfaces.append(repo_root / "examples/README.md")
    forbidden_public_fragments = (
        "dev.cmd",
        "temp/e/<token>",
        "-SkipEnergyPlusWorkflow",
        "-EnergyPlusRoot",
        "-WeatherPath",
        "rebuild/reinstall",
        "docs/development/",
        "../development/",
    )
    for path in public_surfaces:
        content = _read(path)
        for fragment in forbidden_public_fragments:
            if fragment.lower() in content.lower():
                relative = path.relative_to(repo_root)
                raise DocumentationError(
                    f"Public documentation leaks a development workflow in {relative}: "
                    f"{fragment!r}"
                )

    generated = _read(repo_root / "docs/user/user-guide/02-in-out-reference.md")
    marker = (
        "_Generated from the public runtime catalog; "
        "do not edit this chapter directly._"
    )
    if marker not in generated:
        raise DocumentationError("The generated In/Out Reference has no generated-source marker.")
    return len(expected) + 1


def _documentation_surfaces(repo_root: Path) -> tuple[Path, ...]:
    paths = [repo_root / "README.md", repo_root / "CHANGELOG.md", repo_root / "NOTICE.md"]
    paths.extend(sorted((repo_root / "docs").rglob("*.md")))
    paths.append(repo_root / "examples/README.md")
    return tuple(dict.fromkeys(path.resolve() for path in paths))


def _validate_links(repo_root: Path) -> int:
    checked = 0
    failures: list[str] = []
    for source in _documentation_surfaces(repo_root):
        text = _read(source)
        for match in LINK_PATTERN.finditer(text):
            target = match.group("target").strip()
            if target.startswith("<") and target.endswith(">"):
                target = target[1:-1]
            lowered = target.lower()
            if lowered.startswith(("http://", "https://", "mailto:", "#")):
                continue
            local = unquote(target.split("#", 1)[0].split("?", 1)[0])
            if not local:
                continue
            checked += 1
            resolved = (source.parent / local).resolve()
            try:
                resolved.relative_to(repo_root)
            except ValueError:
                failures.append(f"{source.relative_to(repo_root)} -> {target} escapes the repository")
                continue
            if not resolved.exists():
                failures.append(f"{source.relative_to(repo_root)} -> {target} is missing")
    if failures:
        raise DocumentationError("Broken local Markdown links:\n- " + "\n- ".join(failures))
    return checked


def _section(text: str, heading: str, next_heading: str | None = None) -> str:
    start_token = f"## {heading}\n"
    start = text.find(start_token)
    if start < 0:
        raise DocumentationError(f"Food4Rhino sheet is missing section '{heading}'.")
    start += len(start_token)
    if next_heading is None:
        end = len(text)
    else:
        end = text.find(f"## {next_heading}\n", start)
        if end < 0:
            raise DocumentationError(
                f"Food4Rhino section '{heading}' has no following '{next_heading}' section."
            )
    return text[start:end]


def _field(section: str, label: str) -> str:
    pattern = re.compile(
        rf"^### {re.escape(label)}(?:[^\n]*)\n\n```text\n(?P<value>.*?)\n```",
        re.MULTILINE | re.DOTALL,
    )
    match = pattern.search(section)
    if match is None:
        raise DocumentationError(f"Food4Rhino sheet is missing copy field '{label}'.")
    value = match.group("value").strip()
    if not value:
        raise DocumentationError(f"Food4Rhino copy field '{label}' is blank.")
    return value


def _validate_food4rhino(repo_root: Path) -> int:
    path = repo_root / "docs/development/publishing/food4rhino.md"
    text = _read(path)
    try:
        package_spec = json.loads(_read(repo_root / "packaging/package-spec.json"))
    except json.JSONDecodeError as exc:
        raise DocumentationError("Package specification is not valid JSON.") from exc
    publication = package_spec.get("publication")
    expected_publication = {
        "projectLicense": "MIT",
        "projectLicenseOwner": "Gonie-Gonie",
        "projectLicenseOwnerType": "individual",
        "projectLicenseReview": "resolved-2026-08-31",
        "publicSupportEmail": "hyeonggon.jo@snu.ac.kr",
        "publicSupportEmailReview": "resolved-2026-08-31",
        "publicPublicationApprovedByOwner": True,
        "publicPublicationApprovalBasis": "owner-risk-acceptance-2026-08-31",
        "weatherSource": "https://climate.onebuilding.org/",
        "weatherRightsVerified": False,
        "weatherRiskAcceptedByOwner": True,
        "weatherRiskAcceptanceReview": "accepted-2026-08-31",
        "weatherRedistributionStatus": "owner-risk-accepted-unverified",
    }
    if publication != expected_publication:
        raise DocumentationError("Package publication metadata differs from the reviewed contract.")
    if "Publication status: **OWNER APPROVED TO PROCEED" not in text:
        raise DocumentationError("Food4Rhino publication status must record owner approval.")

    shared = _section(text, "Shared Food4Rhino fields", "Shared source and Yak metadata")
    source_metadata = _section(text, "Shared source and Yak metadata", "InvisibleDragon App")
    exact_shared = {
        "Cost": "Free",
        "Website": "https://github.com/Gonie-Gonie/EPlusSimple-Grasshopper",
        "Support Forum": "https://github.com/Gonie-Gonie/EPlusSimple-Grasshopper/issues",
        "Support Email": expected_publication["publicSupportEmail"],
        "License Agreement": expected_publication["projectLicense"],
        "App Platforms": "Windows\nGrasshopper",
        "Release Platforms": (
            "Grasshopper for Rhino 7 for Win\nGrasshopper for Rhino 8 for Win"
        ),
    }
    for label, expected in exact_shared.items():
        actual = _field(shared, label)
        if actual != expected:
            raise DocumentationError(
                f"Food4Rhino field '{label}' must be exactly {expected!r}; found {actual!r}."
            )

    support_email = _field(shared, "Support Email")
    if EMAIL_PATTERN.fullmatch(support_email) is None:
        raise DocumentationError("Food4Rhino Support Email is not a valid confirmed address.")
    if "OWNER_RISK_ACCEPTED_WITHOUT_VERIFIED_WEATHER_PERMISSION" not in text:
        raise DocumentationError("Food4Rhino owner-risk disclosure token is missing.")
    if "does not state or imply" not in text:
        raise DocumentationError("Food4Rhino owner-risk disclosure is incomplete.")
    for contract_value in (
        expected_publication["publicPublicationApprovalBasis"],
        expected_publication["weatherRiskAcceptanceReview"],
        expected_publication["weatherRedistributionStatus"],
        "publicPublicationApprovedByOwner: true",
        "weatherRightsVerified: false",
        "weatherRiskAcceptedByOwner: true",
    ):
        if contract_value not in text:
            raise DocumentationError(
                "Food4Rhino worksheet omits machine-readable publication value: "
                f"{contract_value!r}."
            )

    exact_source_metadata = {
        "Source Code": "https://github.com/Gonie-Gonie/EPlusSimple-Grasshopper",
        "Documentation": (
            "https://github.com/Gonie-Gonie/EPlusSimple-Grasshopper/"
            "blob/main/docs/user/README.md"
        ),
        "Yak Authors": "Gonie-Gonie",
        "SimpleDragon Weather Source": expected_publication["weatherSource"],
    }
    for label, expected in exact_source_metadata.items():
        actual = _field(source_metadata, label)
        if actual != expected:
            raise DocumentationError(
                f"Food4Rhino source value '{label}' must be exactly "
                f"{expected!r}; found {actual!r}."
            )

    if "A `.yak` file is never selected in Food4Rhino's" not in text:
        raise DocumentationError("Food4Rhino sheet does not distinguish Yak linkage from uploads.")
    if expected_publication["weatherRedistributionStatus"] not in text:
        raise DocumentationError("Food4Rhino weather redistribution status is stale.")
    file_link_pattern = re.compile(
        r"^### [^\n]*File / Link[^\n]*\n\n```text\n(?P<value>.*?)\n```",
        re.MULTILINE | re.DOTALL,
    )
    for match in file_link_pattern.finditer(text):
        if ".yak" in match.group("value").lower():
            raise DocumentationError(
                "A Yak artifact is incorrectly presented as a Food4Rhino File / Link value."
            )

    package_spec = json.loads(
        (repo_root / "packaging/package-spec.json").read_text(encoding="utf-8")
    )
    version = str(package_spec["version"])
    products = {str(item["id"]): item for item in package_spec["products"]}
    product_sections = {
        "invisible-dragon": _section(text, "InvisibleDragon App", "SimpleDragon App"),
        "simple-dragon": _section(text, "SimpleDragon App", "Upload sequence after authorization"),
    }
    checked = len(exact_shared) + len(exact_source_metadata) + 1
    for product_id, section in product_sections.items():
        product = products.get(product_id)
        if product is None:
            raise DocumentationError(f"Package spec is missing product '{product_id}'.")
        display_name = str(product["display_name"])
        manifest = _read(repo_root / f"packaging/manifests/{product_id}.yml")
        manifest_contract = (
            f"name: {product_id}",
            f"version: {version}",
            "  - Gonie-Gonie",
            "url: https://github.com/Gonie-Gonie/EPlusSimple-Grasshopper",
            "icon: icon.png",
        )
        for expected in manifest_contract:
            if expected not in manifest:
                raise DocumentationError(
                    f"Yak manifest identity for {display_name} is stale: missing {expected!r}."
                )
        checked += len(manifest_contract)
        if f"<!-- product-id: {product_id} -->" not in section:
            raise DocumentationError(f"Food4Rhino section has no product marker for {product_id}.")

        required_fields = (
            "Title",
            "Short Description",
            "Body",
            "Categories",
            "Project Icon",
            "Create App from Yak - Package Name",
            "Yak Version",
            "Yak-managed Release Title",
            "Yak-managed Release Description",
            "Candidate Artifact Inventory",
            "Optional Portable File Title",
            "Optional Portable File Description",
            "Optional Portable File / Link",
            "Optional Portable Platforms",
            "Yak Keywords",
            "Runtime Requirements",
            "Other Images",
            "Media",
        )
        values = {label: _field(section, label) for label in required_fields}
        checked += len(values)

        expected_scalars = {
            "Title": display_name,
            "Create App from Yak - Package Name": product_id,
            "Yak Version": version,
            "Yak-managed Release Title": f"{display_name} {version}",
            "Optional Portable File Title": f"{display_name} {version} portable plugin",
        }
        for label, expected in expected_scalars.items():
            if values[label] != expected:
                raise DocumentationError(
                    f"Food4Rhino {display_name} field '{label}' is stale: {values[label]!r}."
                )
        if values["Yak-managed Release Description"].startswith("<"):
            raise DocumentationError(
                f"Food4Rhino {display_name} Yak release description is unresolved."
            )

        short_description = values["Short Description"]
        if len(short_description) > 180:
            raise DocumentationError(
                f"Food4Rhino {display_name} Short Description has "
                f"{len(short_description)} characters; maximum is 180."
            )
        count_match = re.search(
            r"^### Short Description[^\n]*? - (?P<count>\d+)/180 characters$",
            section,
            re.MULTILINE,
        )
        if count_match is None or int(count_match.group("count")) != len(short_description):
            raise DocumentationError(
                f"Food4Rhino {display_name} Short Description count label is stale."
            )

        icon = values["Project Icon"]
        if not (repo_root / icon).is_file():
            raise DocumentationError(f"Food4Rhino project icon is missing: {icon}")
        for image in values["Other Images"].splitlines():
            if not (repo_root / image).is_file():
                raise DocumentationError(f"Food4Rhino prepared image is missing: {image}")

        expected_files = {
            f"artifacts/packages/{product_id}/yak/{product_id}-{version}-rh7-win.yak",
            f"artifacts/packages/{product_id}/yak/{product_id}-{version}-rh8-win.yak",
            (
                f"artifacts/packages/{product_id}/portable/"
                f"{product_id}-{version}-portable-plugin-win.zip"
            ),
        }
        if set(values["Candidate Artifact Inventory"].splitlines()) != expected_files:
            raise DocumentationError(
                f"Food4Rhino {display_name} release filenames do not match package-spec.json."
            )

        portable_path = (
            f"artifacts/packages/{product_id}/portable/"
            f"{product_id}-{version}-portable-plugin-win.zip"
        )
        if values["Optional Portable File / Link"] != portable_path:
            raise DocumentationError(
                f"Food4Rhino {display_name} portable upload path is stale."
            )
        expected_platforms = (
            "Grasshopper for Rhino 7 for Win\nGrasshopper for Rhino 8 for Win"
        )
        if values["Optional Portable Platforms"] != expected_platforms:
            raise DocumentationError(
                f"Food4Rhino {display_name} portable platforms are incomplete."
            )

        manifest_keywords_text = manifest.partition("\nkeywords:\n")[2]
        manifest_keywords = {
            line.removeprefix("  - ").strip()
            for line in manifest_keywords_text.splitlines()
            if line.startswith("  - ")
        }
        worksheet_keywords = set(values["Yak Keywords"].splitlines())
        if worksheet_keywords != manifest_keywords:
            raise DocumentationError(
                f"Food4Rhino {display_name} Yak keywords do not match its manifest."
            )
    return checked


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Verify public/development documentation and Food4Rhino metadata."
    )
    parser.add_argument("--repo-root", required=True, type=Path)
    args = parser.parse_args()
    repo_root = args.repo_root.resolve(strict=True)
    hierarchy_count = _validate_hierarchy(repo_root)
    link_count = _validate_links(repo_root)
    metadata_count = _validate_food4rhino(repo_root)
    print(
        "Documentation sources: passed "
        f"({hierarchy_count} hierarchy files, {link_count} local links, "
        f"{metadata_count} Food4Rhino fields)."
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except DocumentationError as exc:
        print(f"Documentation verification failed: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc

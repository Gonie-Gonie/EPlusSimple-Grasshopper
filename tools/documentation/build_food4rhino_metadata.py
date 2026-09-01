"""Build the version-bound Food4Rhino publishing metadata PDF with OODocs."""

from __future__ import annotations

import argparse
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import tempfile
from typing import Any, Sequence


def _load_user_guide_helpers() -> Any:
    """Load the sibling helper explicitly so Python ``-I`` stays supported."""

    helper_path = Path(__file__).resolve().with_name("build_user_guide.py")
    spec = importlib.util.spec_from_file_location(
        "dragons_documentation_build_user_guide",
        helper_path,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load OODocs rendering helpers from {helper_path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


_USER_GUIDE_HELPERS = _load_user_guide_helpers()
UserGuideBuildError = _USER_GUIDE_HELPERS.UserGuideBuildError
_import_chapter = _USER_GUIDE_HELPERS._import_chapter
_load_oodocs = _USER_GUIDE_HELPERS._load_oodocs
render_pdf_only = _USER_GUIDE_HELPERS.render_pdf_only


PACKAGE_SCHEMA = "dragons-grasshopper.package-spec.v3"
EXPECTED_RELEASE_VERSION = "0.1.0"
SOURCE_PATH = Path("docs/development/publishing/food4rhino.md")
EXPECTED_PRODUCT_IDENTITIES = {
    "invisible-dragon": "InvisibleDragon",
    "simple-dragon": "SimpleDragon",
}
EXPECTED_PUBLICATION = {
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
EXPECTED_SECTIONS = (
    "Publication status and field contract",
    "Shared Food4Rhino fields",
    "Shared source and Yak metadata",
    "InvisibleDragon App",
    "SimpleDragon App",
    "Upload sequence after authorization",
)
SAFETY_TOKENS = (
    "OWNER_RISK_ACCEPTED_WITHOUT_VERIFIED_WEATHER_PERMISSION",
)


class Food4RhinoPdfError(RuntimeError):
    """Raised when the canonical worksheet cannot produce a trustworthy PDF."""


def _load_contract(repo_root: Path) -> tuple[str, Path, str, tuple[str, ...], tuple[str, ...]]:
    spec_path = repo_root / "packaging/package-spec.json"
    source_path = repo_root / SOURCE_PATH
    if not spec_path.is_file():
        raise Food4RhinoPdfError(f"Package specification is missing: {spec_path}")
    if not source_path.is_file():
        raise Food4RhinoPdfError(f"Food4Rhino worksheet is missing: {source_path}")
    try:
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise Food4RhinoPdfError(f"Package specification is not valid UTF-8 JSON: {exc}") from exc
    if not isinstance(spec, dict) or spec.get("schema") != PACKAGE_SCHEMA:
        raise Food4RhinoPdfError("Package specification has an unsupported schema.")
    version = spec.get("version")
    if version != EXPECTED_RELEASE_VERSION:
        raise Food4RhinoPdfError(
            f"The first-release documentation guard requires {EXPECTED_RELEASE_VERSION}; "
            f"package-spec declares {version!r}. Make the final version decision in source first."
        )
    products = spec.get("products")
    if not isinstance(products, list):
        raise Food4RhinoPdfError("Package specification products must be an array.")
    identities = {
        item.get("id"): item.get("display_name")
        for item in products
        if isinstance(item, dict)
    }
    if identities != EXPECTED_PRODUCT_IDENTITIES:
        raise Food4RhinoPdfError(
            f"Package products differ from the two release Apps: {identities!r}."
        )
    publication = spec.get("publication")
    if publication != EXPECTED_PUBLICATION:
        raise Food4RhinoPdfError(
            f"Package publication metadata differs from the reviewed contract: {publication!r}."
        )

    try:
        source = source_path.read_text(encoding="utf-8")
    except UnicodeDecodeError as exc:
        raise Food4RhinoPdfError("Food4Rhino worksheet is not valid UTF-8.") from exc
    if not source.strip():
        raise Food4RhinoPdfError("Food4Rhino worksheet is blank.")
    stale_placeholders = ("[TODO]", "<CAPTURE_", "<TODO", "TODO>")
    present_placeholders = tuple(
        placeholder for placeholder in stale_placeholders if placeholder in source
    )
    if present_placeholders:
        raise Food4RhinoPdfError(
            "Food4Rhino worksheet contains stale placeholders: "
            + ", ".join(present_placeholders)
        )

    sections = tuple(
        match.group(1).strip()
        for match in re.finditer(r"^## ([^#\n].*)$", source, re.MULTILINE)
    )
    if sections != EXPECTED_SECTIONS:
        raise Food4RhinoPdfError(
            f"Food4Rhino worksheet sections changed unexpectedly: {sections!r}."
        )
    fields = tuple(
        match.group(1).strip()
        for match in re.finditer(r"^### ([^#\n].*)$", source, re.MULTILINE)
    )
    if not fields:
        raise Food4RhinoPdfError("Food4Rhino field headings are missing.")
    fenced_values = tuple(
        match.group(1).strip()
        for match in re.finditer(r"```text\n(.*?)\n```", source, re.DOTALL)
    )
    if not fenced_values or any(not value for value in fenced_values):
        raise Food4RhinoPdfError("Food4Rhino paste/select/upload blocks are missing or blank.")
    for token in SAFETY_TOKENS:
        if token not in source:
            raise Food4RhinoPdfError(f"Food4Rhino safety token is missing: {token}")
    for value in (
        EXPECTED_PUBLICATION["projectLicense"],
        EXPECTED_PUBLICATION["projectLicenseOwner"],
        EXPECTED_PUBLICATION["projectLicenseOwnerType"],
        EXPECTED_PUBLICATION["publicSupportEmail"],
        EXPECTED_PUBLICATION["publicPublicationApprovalBasis"],
        EXPECTED_PUBLICATION["weatherSource"],
        EXPECTED_PUBLICATION["weatherRiskAcceptanceReview"],
        EXPECTED_PUBLICATION["weatherRedistributionStatus"],
        "publicPublicationApprovedByOwner: true",
        "weatherRightsVerified: false",
        "weatherRiskAcceptedByOwner: true",
    ):
        if value not in source:
            raise Food4RhinoPdfError(
                f"Food4Rhino worksheet does not reflect package publication metadata: {value!r}."
            )
    return version, source_path, source, fields, fenced_values


def _build_document(api: Any, source_path: Path, source: str, version: str) -> Any:
    try:
        chapter = _import_chapter(
            api,
            source_path,
            "Food4Rhino Publishing Worksheet",
            frozenset(),
            source_override=source,
        )
    except UserGuideBuildError as exc:
        raise Food4RhinoPdfError(str(exc)) from exc

    theme = api.Theme(
        typography=api.TypographyDefaults(
            body_font_name="Malgun Gothic",
            monospace_font_name="Consolas",
            title_font_size=23.0,
            body_font_size=9.0,
            heading_sizes=(17.0, 14.0, 11.5, 10.5, 9.5, 9.0),
            caption_font_size=8.0,
        ),
        page_numbers=api.PageNumberDefaults(
            show_page_numbers=True,
            page_number_alignment="right",
            page_number_template="{page}",
            page_number_font_size=8.0,
        ),
        header_footer=api.HeaderFooterDefaults(
            header_left="Food4Rhino Publishing Metadata",
            header_right=f"Dragons {version}",
            footer_left="Gonie-Gonie",
            footer_right="{page}",
            different_first_page=True,
            first_header_left="",
            first_header_right="",
            first_footer_left="",
            first_footer_right="",
            font_size=8.0,
        ),
    )
    title = f"Dragons Food4Rhino Publishing Metadata {version}"
    settings = api.DocumentSettings(
        metadata=api.DocumentMetadata(
            title=title,
            author="Gonie-Gonie",
            subject="Copy, selection, upload, and Yak verification values for Food4Rhino",
            keywords=(
                "Food4Rhino",
                "InvisibleDragon",
                "SimpleDragon",
                "Grasshopper",
                version,
            ),
            description="Version-bound Food4Rhino publishing worksheet for both Dragon Apps.",
        ),
        title_matter=api.TitleMatter(
            subtitle=f"Release metadata and upload references - version {version}",
            authors=(api.Author("Gonie-Gonie"),),
            author_layout=api.AuthorLayout(
                mode="stacked",
                show_affiliations=False,
                show_details=False,
            ),
            cover=api.CoverPage(
                eyebrow="PUBLISHING WORKSHEET",
                organization="Gonie-Gonie",
                footer=f"InvisibleDragon + SimpleDragon {version}",
            ),
        ),
        page_layout=api.PageLayout.portrait(
            api.PageSize.a4(),
            api.PageMargins(top=1.8, right=1.6, bottom=1.8, left=1.6, unit="cm"),
        ),
        theme=theme,
    )
    return api.Document(
        title,
        api.TableOfContents("Contents", max_level=3, show_page_numbers=True),
        api.PageBreak(),
        api.VerticalSpace(10, unit="pt"),
        chapter,
        settings=settings,
    )


def _compact(value: str) -> str:
    return re.sub(r"\s+", "", value).casefold()


def _validate_pdf(
    path: Path,
    version: str,
    fields: tuple[str, ...],
    fenced_values: tuple[str, ...],
) -> None:
    try:
        from pypdf import PdfReader

        reader = PdfReader(path)
        if reader.is_encrypted:
            raise Food4RhinoPdfError("Food4Rhino metadata PDF is encrypted.")
        if not reader.pages:
            raise Food4RhinoPdfError("Food4Rhino metadata PDF has no pages.")
        metadata = reader.metadata
        text = "\n".join(page.extract_text() or "" for page in reader.pages)
    except Food4RhinoPdfError:
        raise
    except Exception as exc:
        raise Food4RhinoPdfError(f"Could not inspect the Food4Rhino metadata PDF: {exc}") from exc

    expected_title = f"Dragons Food4Rhino Publishing Metadata {version}"
    if metadata is None or metadata.title != expected_title or metadata.author != "Gonie-Gonie":
        raise Food4RhinoPdfError("Food4Rhino PDF metadata title/author is incorrect.")
    compact_text = _compact(text)
    required_text = (
        "Food4Rhino publishing worksheet",
        *EXPECTED_SECTIONS,
        *EXPECTED_PRODUCT_IDENTITIES.keys(),
        *EXPECTED_PRODUCT_IDENTITIES.values(),
        version,
        *SAFETY_TOKENS,
        EXPECTED_PUBLICATION["publicPublicationApprovalBasis"],
        EXPECTED_PUBLICATION["weatherRiskAcceptanceReview"],
        EXPECTED_PUBLICATION["weatherRedistributionStatus"],
        "publicPublicationApprovedByOwner: true",
        "weatherRightsVerified: false",
        "weatherRiskAcceptedByOwner: true",
        "does not state or imply",
        *fields,
    )
    missing = [value for value in required_text if _compact(value) not in compact_text]
    if missing:
        raise Food4RhinoPdfError(
            "Food4Rhino metadata PDF is missing required text: " + ", ".join(missing[:8])
        )
    missing_values = [value for value in fenced_values if _compact(value) not in compact_text]
    if missing_values:
        raise Food4RhinoPdfError(
            f"Food4Rhino metadata PDF lost {len(missing_values)} copy/select/upload value block(s)."
        )


def build_food4rhino_pdf(repo_root: Path, output_path: Path) -> Path:
    repo_root = repo_root.resolve(strict=True)
    version, source_path, source, fields, fenced_values = _load_contract(repo_root)
    output_path = output_path if output_path.is_absolute() else repo_root / output_path
    output_path = output_path.resolve()
    expected_name = f"Dragons-Grasshopper-Food4Rhino-Metadata-{version}.pdf"
    if output_path.name != expected_name:
        raise Food4RhinoPdfError(
            f"Food4Rhino PDF filename must be {expected_name!r}; found {output_path.name!r}."
        )

    api = _load_oodocs()
    document = _build_document(api, source_path, source, version)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, staged_name = tempfile.mkstemp(
        prefix=f".{output_path.stem}.staged.", suffix=".pdf", dir=output_path.parent
    )
    os.close(descriptor)
    staged_path = Path(staged_name)
    try:
        staged_path.unlink()
        try:
            render_pdf_only(document, staged_path)
        except UserGuideBuildError as exc:
            raise Food4RhinoPdfError(str(exc)) from exc
        _validate_pdf(staged_path, version, fields, fenced_values)
        os.replace(staged_path, output_path)
    finally:
        if staged_path.exists():
            staged_path.unlink()
    return output_path


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Build the PDF-only Food4Rhino publishing worksheet with OODocs."
    )
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        output = build_food4rhino_pdf(args.repo_root, args.output)
    except (Food4RhinoPdfError, UserGuideBuildError) as exc:
        print(f"Food4Rhino metadata PDF build failed: {exc}", file=sys.stderr)
        return 1
    print(
        json.dumps(
            {"pdf_path": str(output), "version": EXPECTED_RELEASE_VERSION},
            ensure_ascii=False,
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

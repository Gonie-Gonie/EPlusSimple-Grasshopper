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
_technical_manual_theme = _USER_GUIDE_HELPERS._technical_manual_theme
render_pdf_only = _USER_GUIDE_HELPERS.render_pdf_only


PACKAGE_SCHEMA = "dragons-grasshopper.package-spec.v3"
EXPECTED_RELEASE_VERSION = "0.1.2"
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
    "weatherSource": "https://climate.onebuilding.org/",
}
EXPECTED_SECTIONS = (
    "Publication status and field contract",
    "Shared Food4Rhino fields",
    "Shared source and Yak metadata",
    "InvisibleDragon App",
    "SimpleDragon App",
    "Upload sequence",
)
PREVIEW_FIGURES = (
    (
        "InvisibleDragon App",
        "invisibledragon-workflow.png",
        "InvisibleDragon left-to-right workflow preview",
    ),
    (
        "SimpleDragon App",
        "simpledragon-workflow.png",
        "SimpleDragon direct-run workflow preview",
    ),
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
    for value in (
        EXPECTED_PUBLICATION["projectLicense"],
        EXPECTED_PUBLICATION["projectLicenseOwner"],
        EXPECTED_PUBLICATION["publicSupportEmail"],
        EXPECTED_PUBLICATION["weatherSource"],
        "publicPublicationApprovedByOwner: true",
    ):
        if value not in source:
            raise Food4RhinoPdfError(
                f"Food4Rhino worksheet does not reflect package publication metadata: {value!r}."
            )
    return version, source_path, source, fields, fenced_values


def _find_section(api: Any, root: Any, title: str) -> Any | None:
    """Return the first descendant section with the exact plain title."""

    for child in getattr(root, "children", ()):
        if not isinstance(child, api.Section):
            continue
        if child.plain_title().strip() == title:
            return child
        nested = _find_section(api, child, title)
        if nested is not None:
            return nested
    return None


def _add_preview_figures(api: Any, chapter: Any, repo_root: Path) -> None:
    """Add PDF-only workflow previews beside each Other Images upload field."""

    try:
        from PIL import Image
    except ImportError as exc:
        raise Food4RhinoPdfError(
            "Pillow is unavailable in the documentation venv. Run 'dev.cmd setup'."
        ) from exc

    for product_title, filename, caption in PREVIEW_FIGURES:
        product_section = _find_section(api, chapter, product_title)
        if product_section is None:
            raise Food4RhinoPdfError(
                f"Could not find the {product_title!r} section for its PDF preview."
            )
        other_images = _find_section(api, product_section, "Other Images [UPLOAD]")
        if other_images is None:
            raise Food4RhinoPdfError(
                f"Could not find {product_title!r} Other Images for its PDF preview."
            )
        image_path = repo_root / "docs/user/assets/illustrations" / filename
        if not image_path.is_file():
            raise Food4RhinoPdfError(f"Food4Rhino preview image is missing: {image_path}")
        try:
            with Image.open(image_path) as image:
                image.load()
                actual_format = image.format
                actual_size = image.size
        except (OSError, ValueError) as exc:
            raise Food4RhinoPdfError(
                f"Could not inspect Food4Rhino preview image {image_path}: {exc}"
            ) from exc
        if actual_format != "PNG" or actual_size != (1920, 1080):
            raise Food4RhinoPdfError(
                f"Food4Rhino preview {image_path} must be a 1920x1080 PNG; "
                f"found format={actual_format!r}, size={actual_size}."
            )
        other_images.children.append(
            api.Figure(
                image_path,
                caption=caption,
                width=16.8,
                unit="cm",
                placement="here",
                alt_text=caption,
            )
        )


def _build_document(
    api: Any,
    repo_root: Path,
    source_path: Path,
    source: str,
    version: str,
) -> Any:
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
    _add_preview_figures(api, chapter, repo_root)

    theme = _technical_manual_theme(
        api,
        header_left="Food4Rhino Publishing Metadata",
        header_right=f"Dragons {version}",
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
                organization="",
                footer=f"InvisibleDragon + SimpleDragon {version}",
            ),
        ),
        page_layout=api.PageLayout.portrait(
            api.PageSize.a4(),
            api.PageMargins(top=2.0, right=2.0, bottom=2.0, left=2.0, unit="cm"),
        ),
        theme=theme,
    )
    return api.Document(
        title,
        api.FrontMatter(
            api.TableOfContents("Contents", max_level=3, show_page_numbers=True)
        ),
        api.MainMatter(
            api.VerticalSpace(10, unit="pt"),
            chapter,
            start_on_new_page=True,
        ),
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
        image_count = sum(len(page.images) for page in reader.pages)
        page_sizes = tuple(
            (float(page.mediabox.width), float(page.mediabox.height))
            for page in reader.pages
        )
    except Food4RhinoPdfError:
        raise
    except Exception as exc:
        raise Food4RhinoPdfError(f"Could not inspect the Food4Rhino metadata PDF: {exc}") from exc

    expected_title = f"Dragons Food4Rhino Publishing Metadata {version}"
    if metadata is None or metadata.title != expected_title or metadata.author != "Gonie-Gonie":
        raise Food4RhinoPdfError("Food4Rhino PDF metadata title/author is incorrect.")
    if any(
        abs(width - 595.28) > 1.0 or abs(height - 841.89) > 1.0
        for width, height in page_sizes
    ):
        raise Food4RhinoPdfError("Food4Rhino metadata PDF contains a non-A4 page.")
    if image_count < len(PREVIEW_FIGURES):
        raise Food4RhinoPdfError(
            "Food4Rhino metadata PDF lost one or more workflow preview images: "
            f"found {image_count}."
        )
    compact_text = _compact(text)
    required_text = (
        "Food4Rhino publishing worksheet",
        *EXPECTED_SECTIONS,
        *EXPECTED_PRODUCT_IDENTITIES.keys(),
        *EXPECTED_PRODUCT_IDENTITIES.values(),
        version,
        "publicPublicationApprovedByOwner: true",
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
    missing_previews = [
        caption
        for _, _, caption in PREVIEW_FIGURES
        if _compact(caption) not in compact_text
    ]
    if missing_previews:
        raise Food4RhinoPdfError(
            "Food4Rhino metadata PDF lost workflow preview caption(s): "
            + ", ".join(missing_previews)
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
    document = _build_document(api, repo_root, source_path, source, version)
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

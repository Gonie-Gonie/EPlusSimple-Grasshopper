"""Generate deterministic behavioral fixtures from the pinned Python source."""

from __future__ import annotations

import argparse
import hashlib
import importlib.metadata
import json
import os
import re
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from typing import Any


SCHEMA_PREFIX = "goniegonie.python-reference"
REQUIRED_PYTHON = (3, 12, 7)
DEPENDENCIES = (
    "colorama",
    "et_xmlfile",
    "numpy",
    "openpyxl",
    "pandas",
    "python-dateutil",
    "pytz",
    "six",
    "tqdm",
    "tzdata",
)
RUNTIME_ADDRESS_PATTERN = re.compile(r"0x[0-9a-fA-F]{7,16}")


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--upstream-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def write_json(path: Path, value: Any) -> None:
    text = json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        indent=2,
        sort_keys=False,
    )
    write_text(path, text + "\n")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical_key(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, allow_nan=False, sort_keys=True)


def canonicalize_runtime_addresses(text: str) -> tuple[str, int]:
    replacements: dict[str, str] = {}

    def replace(match: re.Match[str]) -> str:
        original = match.group(0)
        if original not in replacements:
            replacements[original] = f"0xAUTO{len(replacements):04d}"
        return replacements[original]

    return RUNTIME_ADDRESS_PATTERN.sub(replace, text), len(replacements)


def enum_value(value: Any) -> Any:
    return getattr(value, "value", value)


def optional_attribute(value: Any, name: str) -> Any:
    return getattr(value, name, None)


def summarize_fenestration(value: Any) -> dict[str, Any]:
    construction = value.construction
    return {
        "id": value.ID,
        "name": value.name,
        "area": value.area,
        "construction_id": construction.ID,
        "construction_name": construction.name,
        "blind": None if optional_attribute(value, "blind") is None else type(value.blind).__name__,
    }


def summarize_surface(value: Any) -> dict[str, Any]:
    construction = value.construction
    return {
        "id": value.ID,
        "name": value.name,
        "area": value.area,
        "azimuth": value.azimuth,
        "surface_type": enum_value(value.type),
        "boundary_condition": enum_value(value.boundary),
        "adjacent_zone_id": None if value.adjacent_zone is None else value.adjacent_zone.ID,
        "construction_id": construction.ID,
        "construction_name": optional_attribute(construction, "name"),
        "construction_type": type(construction).__name__,
        "reflectance": value.reflectance,
        "fenestrations": [summarize_fenestration(item) for item in value.fenestrations],
    }


def summarize_system(value: Any) -> dict[str, Any]:
    return {
        "id": value.ID,
        "name": optional_attribute(value, "name"),
        "type": type(value).__name__,
    }


def summarize_model(model: Any, fixture_sha256: str) -> dict[str, Any]:
    zones = []
    for zone in model.zone:
        zones.append(
            {
                "id": zone.ID,
                "name": zone.name,
                "area": zone.area,
                "height": zone.height,
                "infiltration": zone.infiltration,
                "light_density": zone.light_density,
                "profile_id": zone.profile.ID,
                "profile_name": zone.profile.name,
                "surfaces": [summarize_surface(item) for item in zone.surface],
                "supply_systems": [summarize_system(item) for item in zone.supply_systems],
                "ventilation_systems": [summarize_system(item) for item in zone.ventilation_systems],
            }
        )

    return {
        "schema": f"{SCHEMA_PREFIX}.model-summary.v1",
        "fixture_sha256": fixture_sha256,
        "name": model.name,
        "address": model.address,
        "vintage": model.vintage.isoformat(),
        "is_multifamily_housing": model.is_multifamlily_housing,
        "north_axis": model.north_axis,
        "terrain": model.terrain,
        "climate": model.climate,
        "weather": model.weather,
        "area": model.area,
        "zones": zones,
        "source_systems": [summarize_system(item) for item in model.source_system],
        "photovoltaic_systems": [summarize_system(item) for item in model.pv],
    }


def git_commit(root: Path) -> str:
    result = subprocess.run(
        ["git", "-C", str(root), "rev-parse", "HEAD"],
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return result.stdout.strip()


def generate() -> None:
    args = parse_arguments()
    repository_root = args.repository_root.resolve()
    upstream_root = args.upstream_root.resolve()
    output = args.output.resolve()
    if sys.version_info[:3] != REQUIRED_PYTHON:
        raise SystemExit(
            f"Reference generation requires Python {'.'.join(map(str, REQUIRED_PYTHON))}; "
            f"found {'.'.join(map(str, sys.version_info[:3]))}."
        )
    if os.environ.get("PYTHONHASHSEED") != "0":
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic upstream iteration.")

    lock_path = repository_root / "upstream" / "upstream.lock.json"
    lock = json.loads(lock_path.read_text(encoding="utf-8"))
    actual_commit = git_commit(upstream_root)
    if actual_commit.lower() != str(lock["commit"]).lower():
        raise SystemExit(f"Upstream commit mismatch: expected {lock['commit']}, found {actual_commit}.")

    # Imports deliberately occur only after the stdlib launcher inserts the
    # isolated dependency and upstream source roots.
    import epsimple
    import idragon
    from epsimple.core import (
        FenestrationConstruction,
        GreenRetrofitModel,
        Material,
        Profile,
        SurfaceConstruction,
    )

    expected_epsimple = str(lock["modules"]["epsimple"]["version"])
    expected_idragon = str(lock["modules"]["idragon"]["version"])
    if epsimple.__version__ != expected_epsimple or idragon.__version__ != expected_idragon:
        raise SystemExit(
            "Upstream package version mismatch: "
            f"epsimple={epsimple.__version__}, idragon={idragon.__version__}."
        )

    dependency_versions = {
        package: importlib.metadata.version(package) for package in DEPENDENCIES
    }
    metadata = {
        "schema": f"{SCHEMA_PREFIX}.metadata.v1",
        "upstream_commit": actual_commit,
        "epsimple_version": epsimple.__version__,
        "idragon_version": idragon.__version__,
        "python_version": ".".join(map(str, sys.version_info[:3])),
        "python_hash_seed": 0,
        "dependencies": dependency_versions,
    }
    write_json(output / "metadata.json", metadata)

    database_outputs = (
        (
            "material-database.json",
            "material-database.v1",
            Material.get_DB("__all__", as_dict=True),
        ),
        (
            "surface-construction-database.json",
            "surface-construction-database.v1",
            SurfaceConstruction.get_DB("__all__", as_dict=True),
        ),
        (
            "fenestration-construction-database.json",
            "fenestration-construction-database.v1",
            FenestrationConstruction.get_DB("__all__", as_dict=True),
        ),
        (
            "profile-database.json",
            "profile-database.v1",
            Profile.get_DB("__all__", as_dict=True),
        ),
    )
    for filename, schema_suffix, items in database_outputs:
        # Preserve upstream CSV iteration order. A canonical fingerprint is
        # included so ports can also compare as an order-independent set.
        unordered = sorted((canonical_key(item) for item in items))
        write_json(
            output / filename,
            {
                "schema": f"{SCHEMA_PREFIX}.{schema_suffix}",
                "count": len(items),
                "unordered_sha256": sha256_bytes("\n".join(unordered).encode("utf-8")),
                "items": items,
            },
        )

    fixture_path = repository_root / "fixtures" / "simple-dragon" / "grm" / "ASHRAE 140 modified.grm"
    fixture_hash = sha256_file(fixture_path)
    model = GreenRetrofitModel.from_grjson(str(fixture_path))
    write_json(
        output / "ashrae-140-modified.model.json",
        summarize_model(model, fixture_hash),
    )

    idf = model.to_idf()
    canonical_idf, normalized_address_count = canonicalize_runtime_addresses(str(idf))
    if not canonical_idf.endswith("\n"):
        canonical_idf += "\n"
    idf_path = output / "ashrae-140-modified.idf"
    write_text(idf_path, canonical_idf)

    object_counts = {
        name: len(values) for name, values in idf.items() if len(values) > 0
    }
    write_json(
        output / "ashrae-140-modified.idf-summary.json",
        {
            "schema": f"{SCHEMA_PREFIX}.idf-summary.v1",
            "energyplus_version": str(idf.version),
            "total_object_count": len(idf),
            "populated_object_type_count": len(object_counts),
            "object_counts": object_counts,
            "canonical_idf_sha256": sha256_file(idf_path),
            "canonical_idf_bytes": idf_path.stat().st_size,
            "normalized_runtime_address_count": normalized_address_count,
            "normalization": "first-occurrence mapping of 0x[0-9a-fA-F]{7,16} to 0xAUTO####",
        },
    )

    files = []
    for path in sorted(output.glob("*"), key=lambda candidate: candidate.name):
        if not path.is_file() or path.name == "manifest.json":
            continue
        files.append(
            {
                "path": path.name,
                "bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            }
        )
    write_json(
        output / "manifest.json",
        {
            "schema": f"{SCHEMA_PREFIX}.manifest.v1",
            "upstream_commit": actual_commit,
            "files": files,
        },
    )
    print(f"Generated {len(files) + 1} deterministic reference files in {output}")


if __name__ == "__main__":
    generate()

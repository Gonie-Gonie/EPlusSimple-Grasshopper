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
from datetime import datetime, timedelta
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


def canonical_items_sha256(items: list[dict[str, Any]]) -> str:
    return sha256_bytes(
        "\n".join(canonical_key(item) for item in items).encode("utf-8")
    )


def summarize_surface_construction(value: Any) -> dict[str, Any]:
    return {
        "name": value.name,
        "u_value": value.get_U(),
        "layers": [
            {
                "material": material.name,
                "thickness": thickness,
            }
            for material, thickness in value.layers
        ],
    }


def generate_database_query_oracle(
    upstream_commit: str,
    surface_construction_type: Any,
    fenestration_construction_type: Any,
    surface_types: Any,
    boundary_conditions: Any,
    address_weather_table: Any,
    climate_table: Any,
    address_to_weather: Any,
) -> dict[str, Any]:
    regulation_dates = sorted(surface_construction_type.REGULATION_DATES)
    climates = sorted({key[4] for key in surface_construction_type._DB})
    housing_values = (False, True)
    radiant_values = (False, True)
    surface_type_values = list(surface_types)
    boundary_condition_values = list(boundary_conditions)

    surface_queries: list[dict[str, Any]] = []
    for vintage in regulation_dates:
        for climate in climates:
            for is_multifamily_housing in housing_values:
                for surface_type in surface_type_values:
                    for boundary_condition in boundary_condition_values:
                        for is_radiant_floor in radiant_values:
                            result = surface_construction_type.get_regulated_construction(
                                vintage,
                                surface_type,
                                boundary_condition,
                                climate,
                                is_radiant_floor=is_radiant_floor,
                                is_multifamily_housing=is_multifamily_housing,
                            )
                            surface_queries.append(
                                {
                                    "vintage": vintage.strftime("%Y-%m-%d"),
                                    "climate": climate,
                                    "is_multifamily_housing": is_multifamily_housing,
                                    "surface_type": surface_type.value,
                                    "boundary_condition": boundary_condition.value,
                                    "is_radiant_floor": is_radiant_floor,
                                    "result": summarize_surface_construction(result),
                                }
                            )

    fenestration_queries: list[dict[str, Any]] = []
    for key in fenestration_construction_type._DB.keys():
        result = fenestration_construction_type.get_DB(key)
        fenestration_queries.append(
            {
                "key": {
                    "window_count": key[0],
                    "low_e_glass": key[1],
                    "argon": key[2],
                    "thermal_break": key[3],
                    "frame": key[4],
                    "cavity": key[5],
                },
                "result": {
                    "name": result.name,
                    "u_value": result.u,
                    "solar_heat_gain_coefficient": result.g,
                    "is_transparent": result.is_transparent,
                },
            }
        )

    weather_metadata: list[dict[str, Any]] = []
    for administrative_area, row in address_weather_table.iterrows():
        weather_metadata.append(
            {
                "administrative_area": administrative_area,
                "legal_district_code": str(row["법정동코드"]),
                "terrain": row["terrain"],
                "administrative_latitude": float(row["행정구역위도"]),
                "administrative_longitude": float(row["행정구역경도"]),
                "weather_location": row["기상지역명"],
                "weather_location_type": row["기상지역유형"],
                "weather_latitude": float(row["기상지역위도"]),
                "weather_longitude": float(row["기상지역경도"]),
                "epw_file_name": row["EPW파일명"],
            }
        )

    climate_effective_dates = sorted(
        datetime.strptime(str(value), "%Y%m%d") for value in climate_table.columns
    )
    earliest_climate_date = climate_effective_dates[0]
    boundary_vintages = sorted(
        {
            effective_date + timedelta(days=offset)
            for effective_date in climate_effective_dates
            for offset in (-1, 0, 1)
            if effective_date + timedelta(days=offset) >= earliest_climate_date
        }
    )
    weather_queries: list[dict[str, Any]] = []
    for metadata in weather_metadata:
        administrative_area = metadata["administrative_area"]
        for vintage in boundary_vintages:
            terrain, climate, weather_location, weather_filepath = address_to_weather(
                administrative_area,
                vintage,
            )
            effective_date = max(
                candidate for candidate in climate_effective_dates if candidate <= vintage
            )
            weather_queries.append(
                {
                    "administrative_area": administrative_area,
                    "vintage": vintage.strftime("%Y-%m-%d"),
                    "climate_effective_date": effective_date.strftime("%Y-%m-%d"),
                    "terrain": terrain,
                    "climate_region": climate,
                    "weather_location": weather_location,
                    "epw_file_name": weather_filepath.name,
                }
            )

    return {
        "schema": f"{SCHEMA_PREFIX}.database-query-oracle.v1",
        "upstream_commit": upstream_commit,
        "surface": {
            "regulation_dates": [value.strftime("%Y-%m-%d") for value in regulation_dates],
            "climates": climates,
            "housing_values": list(housing_values),
            "surface_types": [value.value for value in surface_type_values],
            "boundary_conditions": [value.value for value in boundary_condition_values],
            "radiant_values": list(radiant_values),
            "query_count": len(surface_queries),
            "unique_result_count": len(
                {query["result"]["name"] for query in surface_queries}
            ),
            "queries_sha256": canonical_items_sha256(surface_queries),
            "queries": surface_queries,
        },
        "fenestration": {
            "query_count": len(fenestration_queries),
            "queries_sha256": canonical_items_sha256(fenestration_queries),
            "queries": fenestration_queries,
        },
        "weather": {
            "metadata_count": len(weather_metadata),
            "metadata_sha256": canonical_items_sha256(weather_metadata),
            "metadata": weather_metadata,
            "climate_effective_dates": [
                value.strftime("%Y-%m-%d") for value in climate_effective_dates
            ],
            "boundary_vintages": [
                value.strftime("%Y-%m-%d") for value in boundary_vintages
            ],
            "query_count": len(weather_queries),
            "queries_sha256": canonical_items_sha256(weather_queries),
            "queries": weather_queries,
        },
    }


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
        SurfaceBoundaryCondition,
        SurfaceConstruction,
        SurfaceType,
    )
    from epsimple.core.model import (
        ADDR_WEATHER_TABLE,
        CLIMATE_TABLE,
        address_to_weather,
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

    write_json(
        output / "database-query-oracle.json",
        generate_database_query_oracle(
            actual_commit,
            SurfaceConstruction,
            FenestrationConstruction,
            SurfaceType,
            SurfaceBoundaryCondition,
            ADDR_WEATHER_TABLE,
            CLIMATE_TABLE,
            address_to_weather,
        ),
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

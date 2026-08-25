"""Generate the pinned Python 0.7.0 UsageProfile schedule oracle.

Run this through ``bootstrap_reference.py`` so imports resolve exclusively from
the pinned upstream source and dependency tree.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any

from epsimple.core.profile import (
    KoreanUsageProfileExtended,
    Profile,
)


SCHEDULE_NAMES = (
    "heating_setpoint",
    "cooling_setpoint",
    "hvac_availability",
    "occupant",
    "lighting",
    "equipment",
)

RUNTIME_ADDRESS_PATTERN = re.compile(r"0x[0-9a-fA-F]{7,16}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    parser.add_argument("--zone-name", default="ZONE-0x000000")
    return parser.parse_args()


def idf_fields(schedule: Any) -> list[str]:
    obj = schedule.to_idf_object()
    fields = list(obj.data.values())
    fields.extend(getattr(obj, "_IdfObject__extended_input"))
    while fields and fields[-1] is None:
        fields.pop()
    return ["" if value is None else str(value) for value in fields]


def schedule_oracle(schedule: Any, canonicalize: Any) -> dict[str, Any]:
    return {
        "name": canonicalize(schedule.name),
        "type": schedule.type.value,
        "minimum": schedule.min,
        "maximum": schedule.max,
        "idf_fields": [canonicalize(value) for value in idf_fields(schedule)],
    }


def csv_sha256(profile: Any) -> str:
    return hashlib.sha256(Path(profile.datapath).read_bytes()).hexdigest()


def build_oracle(upstream_commit: str, zone_name: str) -> dict[str, Any]:
    profiles = Profile.get_DB("__all__")
    result: dict[str, Any] = {
        "schema": "goniegonie.simpledragon.usage-profile-schedule-oracle.v1",
        "upstream_commit": upstream_commit.lower(),
        "zone_name": zone_name,
        "profile_count": len(profiles),
        "sources": {
            "standard_csv_sha256": csv_sha256(profiles[0]),
            "extended_csv_sha256": csv_sha256(profiles[-1]),
        },
        "profiles": [],
    }

    for profile in profiles:
        dragon_profile = profile.to_dragon()
        replacements: dict[str, str] = {}

        def canonicalize(value: str) -> str:
            def replace(match: re.Match[str]) -> str:
                source = match.group(0)
                if source not in replacements:
                    replacements[source] = f"0xAUTO{len(replacements):04d}"
                return replacements[source]

            return RUNTIME_ADDRESS_PATTERN.sub(replace, value)

        schedules = {
            name: schedule_oracle(getattr(dragon_profile, name), canonicalize)
            for name in SCHEDULE_NAMES
        }

        for purpose in ("occupant", "equipment"):
            schedule = getattr(dragon_profile, purpose)
            normalized_name = (
                f"{schedule.name}_normalized:for:{zone_name}:{purpose}"
            )
            schedules[f"normalized_{purpose}"] = schedule_oracle(
                schedule.normalize_by_max(new_name=normalized_name),
                canonicalize,
            )

        result["profiles"].append(
            {
                "name": profile.name,
                "id": profile.ID,
                "source": (
                    "extended"
                    if isinstance(profile, KoreanUsageProfileExtended)
                    else "standard"
                ),
                "occupied_hours": profile.occupied_hours,
                "operating_days": profile.operating_days,
                "vacations": [
                    {
                        "start": f"{start_month:02d}/{start_day:02d}",
                        "end": f"{end_month:02d}/{end_day:02d}",
                    }
                    for (start_month, start_day), (end_month, end_day)
                    in profile.vacations
                ],
                "occupancy_density": dragon_profile.occupant.max,
                "equipment_power_density": dragon_profile.equipment.max,
                "schedules": schedules,
            }
        )

    return result


def main() -> int:
    args = parse_args()
    result = build_oracle(args.upstream_commit, args.zone_name)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(
        f"Wrote {result['profile_count']} UsageProfile schedule oracles: "
        f"{args.output}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

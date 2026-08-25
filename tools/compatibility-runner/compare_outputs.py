"""Produce a structured cross-language compatibility report."""

from __future__ import annotations

import argparse
import collections
import json
import math
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any


MAX_MISMATCHES = 500
ADDRESS_PATTERN = re.compile(r"0x(?:AUTO\d{4}|[0-9a-f]{7,16})", re.IGNORECASE)
IDD_OBJECT_PATTERN = re.compile(r"^\s*([^\s!\\][^,;]*?)\s*,\s*$")
IDD_FIELD_PATTERN = re.compile(
    r"^\s*((?:[AN]\d+\s*,\s*)*[AN]\d+)\s*([,;])"
    r"(?:\s*\\(?:field|note)(?:\s+.*)?)?\s*$",
    re.IGNORECASE,
)
IDD_DEFAULT_PATTERN = re.compile(r"^\s*\\default(?:\s+(.*?))?\s*$", re.IGNORECASE)
IDD_EXTENSIBLE_PATTERN = re.compile(
    r"^\s*\\extensible\s*:\s*(\d+)(?:\s+.*)?$",
    re.IGNORECASE,
)
IDD_BEGIN_EXTENSIBLE_PATTERN = re.compile(
    r"^\s*\\begin-extensible(?:\s+.*)?$",
    re.IGNORECASE,
)
SCHEDULE_DAY_TYPES = (
    "monday",
    "tuesday",
    "wednesday",
    "thursday",
    "friday",
    "saturday",
    "sunday",
    "holiday",
    "summerdesignday",
    "winterdesignday",
    "customday1",
    "customday2",
)
SCHEDULE_DAY_GROUPS = {
    "alldays": SCHEDULE_DAY_TYPES,
    "weekdays": SCHEDULE_DAY_TYPES[:5],
    "weekends": SCHEDULE_DAY_TYPES[5:7],
}
SCHEDULE_DIRECTIVE_PATTERN = re.compile(
    r"^(through|for|interpolate|until)\s*:\s*(.*?)\s*$",
    re.IGNORECASE,
)
SCHEDULE_DATE_PATTERN = re.compile(r"^(\d{1,2})\s*[-/]\s*(\d{1,2})$")
SCHEDULE_TIME_PATTERN = re.compile(r"^(\d{1,2}):(\d{2})$")

ScheduleDayProfile = tuple[str, tuple[tuple[int, float], ...]]
ScheduleThroughProfile = tuple[
    tuple[int, int],
    tuple[tuple[str, ScheduleDayProfile], ...],
]
ScheduleCompactProfile = tuple[ScheduleThroughProfile, ...]

@dataclass(frozen=True)
class IddObjectDefinition:
    defaults: tuple[str | None, ...]
    extensible_start: int | None
    extensible_size: int | None


IddSchema = dict[str, IddObjectDefinition]
MISSING_IDF_FIELD = object()
SHA256_PATTERN = re.compile(r"^[0-9a-fA-F]{64}$")


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--python-output", type=Path, required=True)
    parser.add_argument("--csharp-output", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--idd", type=Path, required=True)
    parser.add_argument("--runtime-manifest", type=Path, required=True)
    parser.add_argument("--compatibility-exceptions", type=Path, required=True)
    parser.add_argument("--case")
    parser.add_argument("--skip-energyplus", action="store_true")
    parser.add_argument("--allow-differences", action="store_true")
    return parser.parse_args()


def load_registered_exception_ids(path: Path) -> set[str]:
    tracker_root = Path(__file__).resolve().parents[1] / "upstream-tracker"
    tracker_root_text = str(tracker_root)
    if tracker_root_text not in sys.path:
        sys.path.insert(0, tracker_root_text)

    from goniegonie_upstream_tracker.yaml_subset import load_yaml_subset

    source = load_yaml_subset(path)
    if not isinstance(source, list):
        raise ValueError("compatibility exception registry must be a YAML sequence")

    identifiers: set[str] = set()
    for index, item in enumerate(source):
        if not isinstance(item, dict):
            raise ValueError(f"compatibility exception[{index}] must be a mapping")
        identifier = item.get("id")
        if not isinstance(identifier, str) or not identifier:
            raise ValueError(f"compatibility exception[{index}] requires an id")
        if identifier in identifiers:
            raise ValueError(f"duplicate compatibility exception id: {identifier}")
        identifiers.add(identifier)
    return identifiers


def validate_case_exception_references(
    cases: list[dict[str, Any]],
    registered_ids: set[str],
) -> list[str]:
    referenced: set[str] = set()
    for case in cases:
        case_id = str(case.get("id", "<unknown>"))
        stage_scope = case.get("stage_scope")
        if stage_scope is not None and not isinstance(stage_scope, dict):
            raise ValueError(f"case '{case_id}' stage_scope must be a mapping")
        if isinstance(stage_scope, dict) and stage_scope.get("not_verified"):
            exception_id = stage_scope.get("exception_id")
            diagnostic = stage_scope.get("diagnostic")
            if not isinstance(exception_id, str) or not exception_id:
                raise ValueError(
                    f"case '{case_id}' with not_verified evidence requires exception_id"
                )
            if not isinstance(diagnostic, str) or not diagnostic.strip():
                raise ValueError(
                    f"case '{case_id}' with not_verified evidence requires a diagnostic"
                )
            referenced.add(exception_id)

        diagnostic_exceptions = case.get("diagnostic_exceptions", [])
        if not isinstance(diagnostic_exceptions, list):
            raise ValueError(
                f"case '{case_id}' diagnostic_exceptions must be a sequence"
            )
        for index, item in enumerate(diagnostic_exceptions):
            if not isinstance(item, dict):
                raise ValueError(
                    f"case '{case_id}' diagnostic_exceptions[{index}] must be a mapping"
                )
            exception_id = item.get("exception_id")
            if not isinstance(exception_id, str) or not exception_id:
                raise ValueError(
                    f"case '{case_id}' diagnostic_exceptions[{index}] requires exception_id"
                )
            referenced.add(exception_id)

    unknown = sorted(referenced - registered_ids)
    if unknown:
        raise ValueError(
            "compatibility cases reference unregistered exceptions: "
            + ", ".join(unknown)
        )
    return sorted(referenced)


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def sha256_file(path: Path) -> str:
    import hashlib

    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def pinned_runtime_identity(
    compatibility_manifest: dict[str, Any],
    runtime_manifest: dict[str, Any],
) -> dict[str, str]:
    declared = compatibility_manifest["energyplus"]
    expected_version = str(runtime_manifest["energyplus_version"])
    expected_build = str(runtime_manifest["energyplus_build"])
    if str(declared["version"]) != expected_version or str(declared["build"]) != expected_build:
        raise ValueError(
            "Compatibility and runtime manifests declare different EnergyPlus versions/builds"
        )

    identity = {
        "energyplus_exe_sha256": str(runtime_manifest["energyplus_exe_sha256"]),
        "idd_sha256": str(runtime_manifest["energyplus_idd_sha256"]),
        "expandobjects_sha256": str(runtime_manifest["expandobjects_sha256"]),
    }
    for name, value in identity.items():
        if SHA256_PATTERN.fullmatch(value) is None:
            raise ValueError(f"Runtime manifest {name} is not a SHA-256 hash")
        identity[name] = value.casefold()
    return identity


def normalize_text(value: str) -> str:
    return ADDRESS_PATTERN.sub("0xAUTO", " ".join(value.split())).casefold()


def numeric_equal(expected: float, actual: float, absolute: float, relative: float) -> bool:
    difference = abs(expected - actual)
    return difference <= absolute + (relative * max(abs(expected), abs(actual)))


class JsonComparison:
    def __init__(self, absolute: float, relative: float, near_zero: float) -> None:
        self.absolute = absolute
        self.relative = relative
        self.near_zero = near_zero
        self.mismatches: list[dict[str, Any]] = []
        self.total_mismatches = 0
        self.compared_numbers = 0
        self.max_absolute_error = 0.0
        self.max_relative_error = 0.0
        self.max_error_path: str | None = None

    def add(self, path: str, reason: str, expected: Any, actual: Any) -> None:
        self.total_mismatches += 1
        if len(self.mismatches) < MAX_MISMATCHES:
            self.mismatches.append(
                {"path": path, "reason": reason, "expected": expected, "actual": actual}
            )

    def compare(self, expected: Any, actual: Any, path: str = "$") -> None:
        if isinstance(expected, bool) or isinstance(actual, bool):
            if expected is not actual:
                self.add(path, "boolean", expected, actual)
            return
        if isinstance(expected, (int, float)) and isinstance(actual, (int, float)):
            expected_number = float(expected)
            actual_number = float(actual)
            self.compared_numbers += 1
            absolute_error = abs(expected_number - actual_number)
            denominator = max(abs(expected_number), abs(actual_number), self.near_zero)
            relative_error = absolute_error / denominator
            if absolute_error > self.max_absolute_error:
                self.max_absolute_error = absolute_error
                self.max_relative_error = relative_error
                self.max_error_path = path
            if not numeric_equal(expected_number, actual_number, self.absolute, self.relative):
                self.add(path, "numeric_tolerance", expected, actual)
            return
        if isinstance(expected, dict) and isinstance(actual, dict):
            expected_keys = set(expected)
            actual_keys = set(actual)
            for key in sorted(expected_keys - actual_keys):
                self.add(f"{path}.{key}", "missing_key", expected[key], None)
            for key in sorted(actual_keys - expected_keys):
                self.add(f"{path}.{key}", "unexpected_key", None, actual[key])
            for key in sorted(expected_keys & actual_keys):
                self.compare(expected[key], actual[key], f"{path}.{key}")
            return
        if isinstance(expected, list) and isinstance(actual, list):
            if len(expected) != len(actual):
                self.add(path, "array_length", len(expected), len(actual))
            for index, (left, right) in enumerate(zip(expected, actual)):
                self.compare(left, right, f"{path}[{index}]")
            return
        if expected != actual:
            self.add(path, "value", expected, actual)

    def result(self) -> dict[str, Any]:
        return {
            "passed": not self.mismatches,
            "compared_numbers": self.compared_numbers,
            "max_absolute_error": self.max_absolute_error,
            "max_relative_error": self.max_relative_error,
            "max_error_path": self.max_error_path,
            "mismatch_count": self.total_mismatches,
            "reported_mismatch_count": len(self.mismatches),
            "truncated": self.total_mismatches > len(self.mismatches),
            "mismatches": self.mismatches,
        }


def strip_idf_comments(text: str) -> str:
    result: list[str] = []
    quoted = False
    index = 0
    while index < len(text):
        character = text[index]
        if character == '"':
            if quoted and index + 1 < len(text) and text[index + 1] == '"':
                result.extend(['"', '"'])
                index += 2
                continue
            quoted = not quoted
            result.append(character)
            index += 1
            continue
        if character == "!" and not quoted:
            newline = text.find("\n", index)
            if newline < 0:
                break
            result.append("\n")
            index = newline + 1
            continue
        result.append(character)
        index += 1
    return "".join(result)


def split_quoted(text: str, separator: str) -> list[str]:
    values: list[str] = []
    start = 0
    quoted = False
    index = 0
    while index < len(text):
        character = text[index]
        if character == '"':
            if quoted and index + 1 < len(text) and text[index + 1] == '"':
                index += 2
                continue
            quoted = not quoted
        elif character == separator and not quoted:
            values.append(text[start:index])
            start = index + 1
        index += 1
    values.append(text[start:])
    return values


def normalize_idf_field(value: str) -> str:
    result = value.strip()
    if len(result) >= 2 and result[0] == '"' and result[-1] == '"':
        result = result[1:-1].replace('""', '"')
    return ADDRESS_PATTERN.sub("0xAUTO", result).strip()


class IddParseError(ValueError):
    """Raised when IDD defaults cannot be parsed without guessing."""


def parse_idd(path: Path) -> IddSchema:
    """Read positional field defaults from an EnergyPlus IDD.

    The parser intentionally understands only the stable object, field, and
    ``\\default`` forms needed for comparison.  Any malformed occurrence of
    those forms raises instead of silently applying a possibly wrong default.
    """

    schema: IddSchema = {}
    current_name: str | None = None
    current_fields: list[str | None] = []
    current_field_index: int | None = None
    current_field_terminated = False
    current_extensible_start: int | None = None
    current_extensible_size: int | None = None

    def fail(line_number: int, message: str) -> IddParseError:
        return IddParseError(f"{path}:{line_number}: {message}")

    def finish_object(line_number: int) -> None:
        nonlocal current_name, current_fields
        nonlocal current_field_index, current_field_terminated
        nonlocal current_extensible_start, current_extensible_size
        if current_name is None:
            return
        if not current_fields:
            raise fail(line_number, f"IDD object {current_name!r} has no fields")
        if not current_field_terminated:
            raise fail(line_number, f"IDD object {current_name!r} has no terminating field")
        if current_extensible_size is None and current_extensible_start is not None:
            raise fail(line_number, f"IDD object {current_name!r} has no extensible group size")
        if current_extensible_size is not None and current_extensible_start is None:
            # EnergyPlus 24.2 has one legacy object (ComfortViewFactorAngles)
            # with many complete sample groups but no begin marker. Its fixed
            # prefix is the remainder after removing complete groups. Refuse
            # the ambiguous divisible case instead of guessing zero fields.
            inferred_start = len(current_fields) % current_extensible_size
            if inferred_start == 0:
                raise fail(
                    line_number,
                    f"IDD object {current_name!r} has ambiguous extensible metadata",
                )
            current_extensible_start = inferred_start
        key = current_name.casefold()
        if key in schema:
            raise fail(line_number, f"duplicate IDD object {current_name!r}")
        schema[key] = IddObjectDefinition(
            defaults=tuple(current_fields),
            extensible_start=current_extensible_start,
            extensible_size=current_extensible_size,
        )
        current_name = None
        current_fields = []
        current_field_index = None
        current_field_terminated = False
        current_extensible_start = None
        current_extensible_size = None

    lines = path.read_text(encoding="utf-8-sig", errors="strict").splitlines()
    for line_number, raw_line in enumerate(lines, start=1):
        line = raw_line.partition("!")[0].rstrip()
        if not line:
            continue

        object_match = IDD_OBJECT_PATTERN.fullmatch(line)
        if object_match is not None:
            finish_object(line_number)
            current_name = object_match.group(1).strip()
            if not current_name:
                raise fail(line_number, "IDD object name is empty")
            continue

        field_match = IDD_FIELD_PATTERN.fullmatch(line)
        if field_match is not None:
            if current_name is None:
                raise fail(line_number, "IDD field appears outside an object")
            if current_field_terminated:
                raise fail(line_number, f"IDD object {current_name!r} has a field after ';'")
            tokens = re.findall(r"[AN]\d+", field_match.group(1), re.IGNORECASE)
            current_fields.extend(None for _ in tokens)
            current_field_index = len(current_fields) - 1
            current_field_terminated = field_match.group(2) == ";"
            continue

        default_match = IDD_DEFAULT_PATTERN.fullmatch(line)
        if default_match is not None:
            if current_name is None or current_field_index is None:
                raise fail(line_number, "IDD default appears before a field")
            default_value = default_match.group(1)
            if default_value is None or not default_value.strip():
                raise fail(line_number, "IDD default value is empty")
            if current_fields[current_field_index] is not None:
                raise fail(line_number, "IDD field has more than one default")
            current_fields[current_field_index] = normalize_idf_field(default_value)
            continue

        extensible_match = IDD_EXTENSIBLE_PATTERN.fullmatch(line)
        if extensible_match is not None:
            if current_name is None:
                raise fail(line_number, "IDD extensible metadata appears outside an object")
            if current_extensible_size is not None:
                raise fail(line_number, "IDD object has more than one extensible declaration")
            current_extensible_size = int(extensible_match.group(1))
            if current_extensible_size <= 0:
                raise fail(line_number, "IDD extensible group size must be positive")
            continue

        if IDD_BEGIN_EXTENSIBLE_PATTERN.fullmatch(line) is not None:
            if current_name is None or current_field_index is None:
                raise fail(line_number, "IDD begin-extensible appears before a field")
            if current_extensible_start is not None:
                raise fail(line_number, "IDD object has more than one begin-extensible marker")
            current_extensible_start = current_field_index
            continue

        stripped = line.lstrip().casefold()
        if stripped.startswith("\\extensible") or stripped.startswith("\\begin-extensible"):
            raise fail(line_number, "malformed IDD extensible metadata")
        if re.match(r"^\s*(?:[AN]\d+\s*[,;])", line, re.IGNORECASE) is not None:
            raise fail(line_number, "malformed IDD field declaration")

    finish_object(len(lines) + 1)
    if not schema:
        raise IddParseError(f"{path}: no IDD objects were found")
    return schema


def parse_idf(path: Path) -> dict[str, list[list[str]]]:
    text = strip_idf_comments(path.read_text(encoding="utf-8", errors="replace"))
    objects: dict[str, list[list[str]]] = collections.defaultdict(list)
    for record in split_quoted(text, ";"):
        fields = [normalize_idf_field(item) for item in split_quoted(record, ",")]
        if not fields or not fields[0]:
            continue
        objects[fields[0].casefold()].append(fields[1:])
    return dict(objects)


class ScheduleCompactParseError(ValueError):
    """Raised when a Schedule:Compact profile cannot be safely canonicalized."""


def parse_schedule_directive(value: str) -> tuple[str, str] | None:
    match = SCHEDULE_DIRECTIVE_PATTERN.fullmatch(value.strip())
    if match is None:
        return None
    return match.group(1).casefold(), match.group(2).strip()


def parse_schedule_date(value: str) -> tuple[int, int]:
    match = SCHEDULE_DATE_PATTERN.fullmatch(value)
    if match is None:
        raise ScheduleCompactParseError(f"Invalid Through date: {value!r}")
    month = int(match.group(1))
    day = int(match.group(2))
    maximum_days = (31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31)
    if month < 1 or month > 12 or day < 1 or day > maximum_days[month - 1]:
        raise ScheduleCompactParseError(f"Invalid Through date: {value!r}")
    return month, day


def parse_schedule_time(value: str) -> int:
    match = SCHEDULE_TIME_PATTERN.fullmatch(value)
    if match is None:
        raise ScheduleCompactParseError(f"Invalid Until time: {value!r}")
    hour = int(match.group(1))
    minute = int(match.group(2))
    if hour > 24 or minute > 59 or (hour == 24 and minute != 0):
        raise ScheduleCompactParseError(f"Invalid Until time: {value!r}")
    return (hour * 60) + minute


def expand_schedule_day_selector(value: str, assigned: set[str]) -> tuple[str, ...]:
    tokens = [token.casefold() for token in value.split()]
    if not tokens:
        raise ScheduleCompactParseError("For directive has no day selector")
    if "allotherdays" in tokens:
        if len(tokens) != 1:
            raise ScheduleCompactParseError("AllOtherDays must be the only selector in its For directive")
        selected = tuple(day for day in SCHEDULE_DAY_TYPES if day not in assigned)
        if not selected:
            raise ScheduleCompactParseError("AllOtherDays does not select any remaining day type")
        return selected

    selected_values: list[str] = []
    for token in tokens:
        if token in SCHEDULE_DAY_GROUPS:
            selected_values.extend(SCHEDULE_DAY_GROUPS[token])
        elif token in SCHEDULE_DAY_TYPES:
            selected_values.append(token)
        else:
            raise ScheduleCompactParseError(f"Unknown For day selector: {token!r}")
    if len(set(selected_values)) != len(selected_values):
        raise ScheduleCompactParseError("For directive selects a day type more than once")
    if any(day in assigned for day in selected_values):
        raise ScheduleCompactParseError("A day type is assigned by more than one For directive")
    return tuple(selected_values)


def parse_schedule_compact_profile(fields: list[str]) -> ScheduleCompactProfile:
    if len(fields) < 6:
        raise ScheduleCompactParseError("Schedule:Compact has no complete daily profile")

    profile_fields = fields[2:]
    through_profiles: list[ScheduleThroughProfile] = []
    index = 0
    previous_date = (0, 0)
    while index < len(profile_fields):
        directive = parse_schedule_directive(profile_fields[index])
        if directive is None or directive[0] != "through":
            raise ScheduleCompactParseError("Each annual range must start with Through")
        through_date = parse_schedule_date(directive[1])
        if through_date <= previous_date:
            raise ScheduleCompactParseError("Through dates must be strictly increasing")
        previous_date = through_date
        index += 1

        assigned: set[str] = set()
        day_profiles: dict[str, ScheduleDayProfile] = {}
        while index < len(profile_fields):
            directive = parse_schedule_directive(profile_fields[index])
            if directive is not None and directive[0] == "through":
                break
            if directive is None or directive[0] != "for":
                raise ScheduleCompactParseError("Through range must contain a For directive")

            selected_days = expand_schedule_day_selector(directive[1], assigned)
            index += 1
            interpolation = "no"
            if index < len(profile_fields):
                directive = parse_schedule_directive(profile_fields[index])
                if directive is not None and directive[0] == "interpolate":
                    interpolation = directive[1].casefold()
                    if interpolation not in {"average", "linear", "no"}:
                        raise ScheduleCompactParseError(
                            f"Unknown Interpolate mode: {directive[1]!r}"
                        )
                    index += 1

            intervals: list[tuple[int, float]] = []
            previous_time = 0
            while index < len(profile_fields):
                directive = parse_schedule_directive(profile_fields[index])
                if directive is None or directive[0] != "until":
                    break
                until_time = parse_schedule_time(directive[1])
                if until_time <= previous_time:
                    raise ScheduleCompactParseError("Until times must be strictly increasing")
                previous_time = until_time
                index += 1
                if index >= len(profile_fields):
                    raise ScheduleCompactParseError("Until directive has no numeric value")
                if parse_schedule_directive(profile_fields[index]) is not None:
                    raise ScheduleCompactParseError("Until directive has no numeric value")
                try:
                    schedule_value = float(profile_fields[index])
                except ValueError as error:
                    raise ScheduleCompactParseError(
                        f"Invalid schedule value: {profile_fields[index]!r}"
                    ) from error
                if not math.isfinite(schedule_value):
                    raise ScheduleCompactParseError(
                        f"Invalid schedule value: {profile_fields[index]!r}"
                    )
                intervals.append((until_time, schedule_value))
                index += 1

            if not intervals or intervals[-1][0] != 24 * 60:
                raise ScheduleCompactParseError("Each For profile must end at 24:00")
            day_profile = (interpolation, tuple(intervals))
            for day in selected_days:
                assigned.add(day)
                day_profiles[day] = day_profile

        if assigned != set(SCHEDULE_DAY_TYPES):
            missing = ", ".join(day for day in SCHEDULE_DAY_TYPES if day not in assigned)
            raise ScheduleCompactParseError(f"Through range leaves day types unassigned: {missing}")
        through_profiles.append(
            (
                through_date,
                tuple((day, day_profiles[day]) for day in SCHEDULE_DAY_TYPES),
            )
        )

    return tuple(through_profiles)


def schedule_compact_profiles_equal(
    expected: ScheduleCompactProfile,
    actual: ScheduleCompactProfile,
    absolute: float,
    relative: float,
) -> bool:
    if len(expected) != len(actual):
        return False
    for left_through, right_through in zip(expected, actual):
        if left_through[0] != right_through[0]:
            return False
        left_days = left_through[1]
        right_days = right_through[1]
        if len(left_days) != len(right_days):
            return False
        for left_day, right_day in zip(left_days, right_days):
            if left_day[0] != right_day[0] or left_day[1][0] != right_day[1][0]:
                return False
            left_intervals = left_day[1][1]
            right_intervals = right_day[1][1]
            if len(left_intervals) != len(right_intervals):
                return False
            for left_interval, right_interval in zip(left_intervals, right_intervals):
                if left_interval[0] != right_interval[0]:
                    return False
                if not numeric_equal(
                    left_interval[1],
                    right_interval[1],
                    absolute,
                    relative,
                ):
                    return False
    return True


def render_schedule_compact_profile(profile: ScheduleCompactProfile) -> str:
    return json.dumps(profile, ensure_ascii=False, separators=(",", ":"))


def field_difference(
    expected: list[str],
    actual: list[str],
    absolute: float,
    relative: float,
    defaults: tuple[str | None, ...] | None = None,
    trailing_blank_limit: int = 0,
) -> tuple[int, list[tuple[int, str, str]]]:
    differences: list[tuple[int, str, str]] = []
    width = max(len(expected), len(actual))
    for index in range(width):
        left: str | object = (
            expected[index] if index < len(expected) else MISSING_IDF_FIELD
        )
        right: str | object = actual[index] if index < len(actual) else MISSING_IDF_FIELD
        # IDF records end at the semicolon. A trailing empty field and a field
        # omitted after the final comma both supply no value to EnergyPlus.
        # This is safe only at the shorter record's tail; later non-empty
        # fields are still visited and reported below.
        if index < trailing_blank_limit and (
            (left is MISSING_IDF_FIELD and right == "")
            or (right is MISSING_IDF_FIELD and left == "")
        ):
            continue
        default = defaults[index] if defaults is not None and index < len(defaults) else None
        normalized_left = default if default is not None and (
            left is MISSING_IDF_FIELD or left == ""
        ) else left
        normalized_right = default if default is not None and (
            right is MISSING_IDF_FIELD or right == ""
        ) else right

        if normalized_left is MISSING_IDF_FIELD or normalized_right is MISSING_IDF_FIELD:
            equal = normalized_left is normalized_right
        else:
            assert isinstance(normalized_left, str)
            assert isinstance(normalized_right, str)
            try:
                left_number = float(normalized_left)
                right_number = float(normalized_right)
            except ValueError:
                equal = normalize_text(normalized_left) == normalize_text(normalized_right)
            else:
                equal = numeric_equal(left_number, right_number, absolute, relative)
        if not equal:
            differences.append(
                (
                    index,
                    "<omitted>" if left is MISSING_IDF_FIELD else str(left),
                    "<omitted>" if right is MISSING_IDF_FIELD else str(right),
                )
            )
    return len(differences), differences


def trailing_blank_limit(
    definition: IddObjectDefinition | None,
    expected_count: int,
    actual_count: int,
) -> int:
    if definition is None:
        return 0
    if definition.extensible_start is None:
        return len(definition.defaults)

    start = definition.extensible_start
    size = definition.extensible_size
    if size is None:
        raise IddParseError("An extensible IDD object has no extensible group size")

    shorter_count = min(expected_count, actual_count)
    populated = max(0, shorter_count - start)
    remainder = populated % size
    if remainder == 0:
        # Do not erase a wholly blank additional extensible group. Only fixed
        # fields before the first group may normalize omission to a blank.
        return start

    # EnergyPlus accepts a final, partially populated extensible group when its
    # remaining optional fields are omitted. Treat explicit blank padding up to
    # that same group's boundary as equivalent, but no farther.
    return shorter_count + (size - remainder)


def schedule_compact_difference(
    expected: list[str],
    actual: list[str],
    absolute: float,
    relative: float,
    defaults: tuple[str | None, ...] | None = None,
    trailing_blank_limit: int = 0,
) -> tuple[int, list[tuple[int, str, str]]]:
    try:
        expected_profile = parse_schedule_compact_profile(expected)
        actual_profile = parse_schedule_compact_profile(actual)
    except ScheduleCompactParseError:
        return field_difference(
            expected,
            actual,
            absolute,
            relative,
            defaults,
            trailing_blank_limit,
        )

    header_defaults = defaults[:2] if defaults is not None else None
    _, differences = field_difference(
        expected[:2],
        actual[:2],
        absolute,
        relative,
        header_defaults,
        min(trailing_blank_limit, 2),
    )
    if not schedule_compact_profiles_equal(
        expected_profile,
        actual_profile,
        absolute,
        relative,
    ):
        differences.append(
            (
                2,
                render_schedule_compact_profile(expected_profile),
                render_schedule_compact_profile(actual_profile),
            )
        )
    return len(differences), differences


def compare_idf(
    expected_path: Path,
    actual_path: Path,
    absolute: float,
    relative: float,
    idd_schema: IddSchema | None = None,
) -> dict[str, Any]:
    expected = parse_idf(expected_path)
    actual = parse_idf(actual_path)
    mismatches: list[dict[str, Any]] = []
    mismatch_count = 0

    def add_mismatch(value: dict[str, Any]) -> None:
        nonlocal mismatch_count
        mismatch_count += 1
        if len(mismatches) < MAX_MISMATCHES:
            mismatches.append(value)

    expected_counts = {key: len(value) for key, value in sorted(expected.items())}
    actual_counts = {key: len(value) for key, value in sorted(actual.items())}
    all_types = sorted(set(expected) | set(actual))
    matched_objects = 0
    for object_type in all_types:
        left_items = expected.get(object_type, [])
        unmatched = list(actual.get(object_type, []))
        if len(left_items) != len(unmatched):
            add_mismatch(
                {
                    "path": f"$.{object_type}",
                    "reason": "object_count",
                    "expected": len(left_items),
                    "actual": len(unmatched),
                }
            )
        for object_index, left in enumerate(left_items):
            if not unmatched:
                break
            difference_function = (
                schedule_compact_difference
                if object_type == "schedule:compact"
                else field_difference
            )
            definition = idd_schema.get(object_type) if idd_schema is not None else None
            defaults = definition.defaults if definition is not None else None
            candidates = [
                difference_function(
                    left,
                    right,
                    absolute,
                    relative,
                    defaults,
                    trailing_blank_limit(definition, len(left), len(right)),
                )
                for right in unmatched
            ]
            best_index = min(range(len(candidates)), key=lambda index: candidates[index][0])
            difference_count, differences = candidates[best_index]
            unmatched.pop(best_index)
            matched_objects += 1
            for field_index, expected_value, actual_value in differences:
                add_mismatch(
                    {
                        "path": f"$.{object_type}[{object_index}].fields[{field_index}]",
                        "reason": "field",
                        "expected": expected_value,
                        "actual": actual_value,
                    }
                )
    return {
        "passed": not mismatches,
        "expected_object_count": sum(expected_counts.values()),
        "actual_object_count": sum(actual_counts.values()),
        "matched_object_count": matched_objects,
        "expected_object_types": expected_counts,
        "actual_object_types": actual_counts,
        "mismatch_count": mismatch_count,
        "reported_mismatch_count": len(mismatches),
        "truncated": mismatch_count > len(mismatches),
        "mismatches": mismatches,
        "comparison": (
            "order-independent object-type grouping with tolerant field matching and "
            "pinned-IDD omitted-default plus trailing-empty normalization"
            if idd_schema is not None
            else "order-independent object-type grouping with tolerant field matching"
        ),
    }


def compare_warnings(
    expected_path: Path,
    actual_path: Path,
    allowed_delta: int,
    diagnostic_exceptions: list[dict[str, Any]] | None = None,
) -> dict[str, Any]:
    expected = load_json(expected_path)
    actual = load_json(actual_path)
    diagnostic_exceptions = diagnostic_exceptions or []
    mismatches: list[dict[str, Any]] = []
    for severity in ("warning", "severe", "fatal"):
        left = int(expected["summary"][severity])
        right = int(actual["summary"][severity])
        if abs(left - right) > allowed_delta:
            mismatches.append(
                {
                    "path": f"$.summary.{severity}",
                    "reason": "count_delta",
                    "expected": left,
                    "actual": right,
                }
            )
    expected_items = collections.Counter(
        (str(item["severity"]).casefold(), normalize_text(str(item["title"])))
        for item in expected["items"]
    )
    actual_items = collections.Counter(
        (str(item["severity"]).casefold(), normalize_text(str(item["title"])))
        for item in actual["items"]
    )
    allowed_items: collections.Counter[tuple[str, str]] = collections.Counter()
    for item in diagnostic_exceptions:
        severity = str(item.get("severity", "")).casefold()
        title = normalize_text(str(item.get("title", "")))
        count = item.get("count")
        exception_id = str(item.get("exception_id", ""))
        if severity not in {"severe", "fatal"}:
            raise ValueError("diagnostic_exceptions severity must be severe or fatal")
        if not title or not exception_id or not isinstance(count, int) or isinstance(count, bool) or count <= 0:
            raise ValueError("diagnostic_exceptions require exception_id, title, and a positive integer count")
        allowed_items[(severity, title)] += count

    for item, allowed_count in sorted(allowed_items.items()):
        expected_count = expected_items[item]
        actual_count = actual_items[item]
        if expected_count != allowed_count or actual_count != allowed_count:
            mismatches.append(
                {
                    "path": "$.items",
                    "reason": "registered_diagnostic_count",
                    "expected": [*item, allowed_count],
                    "actual": {
                        "python": expected_count,
                        "csharp": actual_count,
                    },
                }
            )

    for engine, items in (("python", expected_items), ("csharp", actual_items)):
        severe_or_fatal = collections.Counter(
            {item: count for item, count in items.items() if item[0] in {"severe", "fatal"}}
        )
        for item, count in sorted((severe_or_fatal - allowed_items).items()):
            mismatches.append(
                {
                    "path": "$.items",
                    "reason": "disallowed_nonzero_severity",
                    "expected": None,
                    "actual": [engine, *item, count],
                }
            )
    for item, count in sorted((expected_items - actual_items).items()):
        mismatches.append(
            {"path": "$.items", "reason": "missing_warning", "expected": [*item, count], "actual": None}
        )
    for item, count in sorted((actual_items - expected_items).items()):
        mismatches.append(
            {"path": "$.items", "reason": "unexpected_warning", "expected": None, "actual": [*item, count]}
        )
    return {
        "passed": not mismatches,
        "expected_summary": expected["summary"],
        "actual_summary": actual["summary"],
        "diagnostic_exceptions": diagnostic_exceptions,
        "mismatch_count": len(mismatches),
        "reported_mismatch_count": min(len(mismatches), MAX_MISMATCHES),
        "truncated": len(mismatches) > MAX_MISMATCHES,
        "mismatches": mismatches[:MAX_MISMATCHES],
    }


def summarize_limitations(results: list[dict[str, Any]]) -> dict[str, Any]:
    limited_cases = [
        item
        for item in results
        if isinstance(item.get("stage_scope"), dict)
        and bool(item["stage_scope"].get("not_verified"))
    ]
    exception_ids = sorted(
        {
            str(item["stage_scope"].get("exception_id"))
            for item in limited_cases
            if item["stage_scope"].get("exception_id")
        }
    )
    diagnostic_exceptions = [
        exception
        for item in results
        for exception in (item.get("diagnostic_exceptions") or [])
    ]
    return {
        "limitation_count": len(limited_cases),
        "limitation_exception_ids": exception_ids,
        "diagnostic_exception_count": len(diagnostic_exceptions),
        "diagnostic_exception_ids": sorted(
            {str(item["exception_id"]) for item in diagnostic_exceptions}
        ),
    }


def check_input_identity(
    python_metadata: dict[str, Any],
    csharp_metadata: dict[str, Any],
    pinned_runtime: dict[str, str] | None = None,
) -> dict[str, Any]:
    pairs = {
        "grm_sha256": (
            python_metadata["inputs"]["grm"]["sha256"],
            csharp_metadata["inputs"]["grm"]["sha256"],
        ),
        "weather_sha256": (
            python_metadata["inputs"]["weather"]["sha256"],
            csharp_metadata["inputs"]["weather"]["sha256"],
        ),
        "energyplus_exe_sha256": (
            python_metadata["runtime"]["energyplus_exe_sha256"],
            csharp_metadata["runtime"]["energyplus_exe_sha256"],
        ),
        "idd_sha256": (
            python_metadata["runtime"]["idd_sha256"],
            csharp_metadata["runtime"]["idd_sha256"],
        ),
        "expandobjects_sha256": (
            python_metadata["runtime"]["expandobjects_sha256"],
            csharp_metadata["runtime"]["expandobjects_sha256"],
        ),
    }
    csharp_roundtrip = python_metadata.get("inputs", {}).get("csharp_roundtrip")
    if csharp_roundtrip is not None:
        roundtrip_output = next(
            (
                item for item in csharp_metadata.get("outputs", [])
                if str(item.get("path", "")).casefold() == "roundtrip.grm"
            ),
            None,
        )
        pairs["csharp_roundtrip_grm_sha256"] = (
            str(csharp_roundtrip["sha256"]),
            str(roundtrip_output["sha256"]) if roundtrip_output is not None else "<missing>",
        )
    mismatches: list[dict[str, Any]] = []
    identities: dict[str, Any] = {}
    for key, (left, right) in pairs.items():
        pinned = pinned_runtime.get(key) if pinned_runtime is not None else None
        identities[key] = {"python": left, "csharp": right, "pinned": pinned}
        if left.casefold() != right.casefold() or (
            pinned is not None
            and (left.casefold() != pinned.casefold() or right.casefold() != pinned.casefold())
        ):
            mismatches.append(
                {"identity": key, "python": left, "csharp": right, "pinned": pinned}
            )
    return {"passed": not mismatches, "identities": identities, "mismatches": mismatches}


def compare_case(
    case: dict[str, Any],
    manifest: dict[str, Any],
    python_root: Path,
    csharp_root: Path,
    skip_energyplus: bool = False,
    idd_schema: IddSchema | None = None,
    pinned_runtime: dict[str, str] | None = None,
) -> dict[str, Any]:
    case_id = str(case["id"])
    python_case = python_root / case_id
    csharp_case = csharp_root / case_id
    tolerance = manifest["tolerances"]
    checks: dict[str, Any] = {}
    pinned_identity = dict(pinned_runtime or {})
    if case.get("input_grm_sha256") is not None:
        pinned_identity["grm_sha256"] = str(case["input_grm_sha256"])
    if case.get("weather_sha256") is not None:
        pinned_identity["weather_sha256"] = str(case["weather_sha256"])
    checks["input_identity"] = check_input_identity(
        load_json(python_case / "metadata.json"),
        load_json(csharp_case / "metadata.json"),
        pinned_identity,
    )
    checks["authoring_idf"] = compare_idf(
        python_case / "authoring.idf",
        csharp_case / "authoring.idf",
        float(tolerance["idf_absolute"]),
        float(tolerance["idf_relative"]),
        idd_schema,
    )
    checks["expanded_idf"] = compare_idf(
        python_case / "expanded.idf",
        csharp_case / "expanded.idf",
        float(tolerance["idf_absolute"]),
        float(tolerance["idf_relative"]),
        idd_schema,
    )
    if "grm_cross_read" in case["stages"]:
        checks["grm_cross_read"] = compare_idf(
            python_case / "authoring.idf",
            python_case / "csharp-roundtrip-authoring.idf",
            float(tolerance["idf_absolute"]),
            float(tolerance["idf_relative"]),
            idd_schema,
        )
    skipped_stages: list[str] = []
    if "grr" in case["stages"] and not skip_energyplus:
        comparison = JsonComparison(
            float(tolerance["grr_absolute"]),
            float(tolerance["grr_relative"]),
            float(tolerance["near_zero"]),
        )
        comparison.compare(
            load_json(python_case / "result.grr"),
            load_json(csharp_case / "result.grr"),
        )
        checks["grr"] = comparison.result()
    elif "grr" in case["stages"]:
        skipped_stages.append("grr")
    if "warnings" in case["stages"] and not skip_energyplus:
        checks["warnings"] = compare_warnings(
            python_case / "warnings.json",
            csharp_case / "warnings.json",
            int(tolerance["warning_count_delta"]),
            case.get("diagnostic_exceptions"),
        )
    elif "warnings" in case["stages"]:
        skipped_stages.append("warnings")
    if "energyplus" in case["stages"] and skip_energyplus:
        skipped_stages.append("energyplus")
    executed_stages = [
        stage for stage in case["stages"]
        if stage not in skipped_stages
    ]
    return {
        "id": case_id,
        "stage_scope": case.get("stage_scope"),
        "diagnostic_exceptions": case.get("diagnostic_exceptions", []),
        "declared_stages": case["stages"],
        "executed_stages": executed_stages,
        "skipped_stages": skipped_stages,
        "skip_count": len(skipped_stages),
        "passed": not skipped_stages
        and all(check["passed"] for check in checks.values()),
        "checks": checks,
    }


def main() -> None:
    args = parse_arguments()
    manifest = load_json(args.manifest)
    runtime_manifest = load_json(args.runtime_manifest)
    pinned_runtime = pinned_runtime_identity(manifest, runtime_manifest)
    reporter_idd_sha256 = sha256_file(args.idd)
    if reporter_idd_sha256.casefold() != pinned_runtime["idd_sha256"]:
        raise SystemExit(
            "Pinned EnergyPlus IDD hash mismatch: expected "
            f"{pinned_runtime['idd_sha256']}, found {reporter_idd_sha256}"
        )
    idd_schema = parse_idd(args.idd)
    cases = [
        item for item in manifest["cases"]
        if args.case is None or str(item["id"]) == args.case
    ]
    if not cases:
        raise SystemExit(f"Unknown compatibility case: {args.case}")
    registered_exception_ids = load_registered_exception_ids(
        args.compatibility_exceptions
    )
    referenced_exception_ids = validate_case_exception_references(
        cases,
        registered_exception_ids,
    )
    results = [
        compare_case(
            item,
            manifest,
            args.python_output,
            args.csharp_output,
            args.skip_energyplus,
            idd_schema,
            pinned_runtime,
        )
        for item in cases
    ]
    passed_count = sum(item["passed"] for item in results)
    skip_count = sum(item["skip_count"] for item in results)
    limitation_summary = summarize_limitations(results)
    report = {
        "schema": "goniegonie.dragons.engineering-compatibility-report.v1",
        "upstream_commit": manifest["upstream_commit"],
        "energyplus": manifest["energyplus"],
        "tolerances": manifest["tolerances"],
        "declared_case_count": len(cases),
        "executed_case_count": len(results),
        "passed_case_count": passed_count,
        "failed_case_count": len(results) - passed_count,
        "skip_count": skip_count,
        **limitation_summary,
        "referenced_exception_ids": referenced_exception_ids,
        "exception_registry_sha256": sha256_file(args.compatibility_exceptions),
        "passed": passed_count == len(results) and skip_count == 0,
        "cases": results,
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(
        json.dumps(report, ensure_ascii=False, allow_nan=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(
        f"Compatibility report: {passed_count}/{len(results)} cases passed; "
        f"skip={skip_count}; {args.report}"
    )
    if not report["passed"] and not args.allow_differences:
        raise SystemExit(1)


if __name__ == "__main__":
    main()

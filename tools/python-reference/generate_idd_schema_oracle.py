r"""Generate a deterministic, full-fidelity EnergyPlus IDD regression oracle.

The pinned idragon 0.7.0 ``IDD.read_idd`` implementation cannot parse an
EnergyPlus 24.2 dictionary containing exclusive numeric bounds: it passes the
raw string to ``math.nextafter``. It also intentionally discards metadata such
as IP units. This stdlib-only parser therefore reads the configured, hash-
verified Energy+.idd directly and preserves every property represented by the
C# ``IddSchema`` model. Because that parser is deliberately mirrored by the C#
implementation, it is a regression snapshot rather than an independent source
of truth. Shared semantics are independently checked against EnergyPlus's
official ``Energy+.schema.epJSON`` before the oracle is written.

EnergyPlus expands some extensible declarations into thousands of physical
placeholder tokens. Once the ``\begin-extensible`` prototype is complete, both
the regression oracle and the production model retain that single prototype
group and resolve later positions through the declared group size.
"""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import os
import re
import sys
from pathlib import Path
from typing import Any


ORACLE_SCHEMA = "dragons.energyplus-idd-schema.v1"
REQUIRED_PYTHON = (3, 12, 7)
EPJSON_SCHEMA_DRAFT = "https://json-schema.org/draft-07/schema#"
FIELD_PATTERN = re.compile(
    r"^\s*(?P<fields>(?:[AN]\d+\s*[,;]\s*)+)(?P<directives>.*)$",
    re.IGNORECASE,
)
FIELD_TOKEN_PATTERN = re.compile(r"(?P<token>[AN]\d+)\s*(?P<delimiter>[,;])", re.IGNORECASE)
VERSION_PATTERN = re.compile(r"^\s*!IDD_Version\s+(?P<value>\S+)", re.IGNORECASE)
BUILD_PATTERN = re.compile(r"^\s*!IDD_BUILD\s+(?P<value>\S+)", re.IGNORECASE)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--idd", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    parser.add_argument("--expected-sha256", required=True)
    parser.add_argument("--epjson-schema", type=Path, required=True)
    parser.add_argument("--expected-epjson-sha256", required=True)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--expected-build", required=True)
    return parser.parse_args()


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def optional(value: str | None) -> str | None:
    if value is None:
        return None
    result = value.strip()
    return result or None


def parse_directive(text: str, line_number: int) -> tuple[str, str]:
    if len(text) < 2 or text[0] != "\\":
        raise ValueError(f"Invalid IDD directive at line {line_number}.")

    index = 1
    while index < len(text) and (text[index].isalnum() or text[index] in "-<>"):
        index += 1
    if index == 1:
        raise ValueError(f"Empty IDD directive at line {line_number}.")

    name = text[1:index]
    if index < len(text) and text[index] == ":":
        index += 1
        end = index
        while end < len(text) and not text[end].isspace():
            end += 1
        value = text[index:end]
    else:
        value = text[index:].strip()
    return name, value.strip()


def add_directive(target: dict[str, list[str]], name: str, value: str) -> None:
    target.setdefault(name, []).append(value)


def new_object(name: str, group: str, position: int) -> dict[str, Any]:
    return {
        "position": position,
        "name": name.strip(),
        "group": group.strip(),
        "memo": [],
        "is_unique": False,
        "is_required": False,
        "minimum_fields": 0,
        "extensible_group_size": 0,
        "extensible_start_index": None,
        "format": None,
        "obsolete_message": None,
        "additional_directives": {},
        "fields": [],
    }


def new_field(token: str, position: int) -> dict[str, Any]:
    return {
        "token": token.upper(),
        "position": position,
        "kind": "numeric" if token.upper().startswith("N") else "alpha",
        "name": "",
        "notes": [],
        "units": None,
        "ip_units": None,
        "units_based_on_field": None,
        "is_required": False,
        "begins_extensible": False,
        "is_deprecated": False,
        "is_autosizable": False,
        "is_autocalculatable": False,
        "retains_case": False,
        "default_value": None,
        "data_type": "unspecified",
        "choices": [],
        "object_lists": [],
        "external_list": None,
        "references": [],
        "reference_class_names": [],
        "minimum": None,
        "maximum": None,
        "additional_directives": {},
    }


def parse_non_negative_integer(name: str, value: str) -> int:
    if not value.isdigit():
        raise ValueError(f"Invalid value for \\{name}: {value!r}.")
    result = int(value)
    if result < 0:
        raise ValueError(f"Invalid value for \\{name}: {value!r}.")
    return result


def parse_number(name: str, value: str) -> float:
    try:
        return float(value)
    except ValueError as exception:
        raise ValueError(f"Invalid value for \\{name}: {value!r}.") from exception


def parse_bound(name: str, value: str, exclusive_operator: str) -> dict[str, Any]:
    text = value.lstrip()
    inclusive = not text.startswith(exclusive_operator)
    if not inclusive:
        text = text[1:].lstrip()
    number = parse_number(name, text)
    return {"value": number, "is_inclusive": inclusive}


def apply_object_directive(target: dict[str, Any], name: str, value: str) -> None:
    normalized = name.lower()
    if normalized in ("memo", "note"):
        target["memo"].append(value)
    elif normalized == "unique-object":
        target["is_unique"] = True
    elif normalized == "required-object":
        target["is_required"] = True
    elif normalized == "min-fields":
        target["minimum_fields"] = parse_non_negative_integer(name, value)
    elif normalized == "extensible":
        target["extensible_group_size"] = parse_non_negative_integer(name, value)
    elif normalized == "format":
        target["format"] = optional(value)
    elif normalized == "obsolete":
        target["obsolete_message"] = optional(value)
    else:
        add_directive(target["additional_directives"], name, value)


def apply_field_directive(target: dict[str, Any], name: str, value: str) -> None:
    normalized = name.lower()
    if normalized == "field":
        target["name"] = value
    elif normalized in ("note", "memo"):
        target["notes"].append(value)
    elif normalized in ("required-field", "required"):
        target["is_required"] = True
    elif normalized == "begin-extensible":
        target["begins_extensible"] = True
    elif normalized == "units":
        target["units"] = optional(value)
    elif normalized == "ip-units":
        target["ip_units"] = optional(value)
    elif normalized == "unitsbasedonfield":
        target["units_based_on_field"] = optional(value)
    elif normalized == "minimum":
        target["minimum"] = parse_bound(name, value, ">")
    elif normalized == "minimum>":
        target["minimum"] = {
            "value": parse_number(name, value),
            "is_inclusive": False,
        }
    elif normalized == "maximum":
        target["maximum"] = parse_bound(name, value, "<")
    elif normalized == "maximum<":
        target["maximum"] = {
            "value": parse_number(name, value),
            "is_inclusive": False,
        }
    elif normalized == "default":
        target["default_value"] = optional(value)
    elif normalized == "deprecated":
        target["is_deprecated"] = True
    elif normalized == "autosizable":
        target["is_autosizable"] = True
    elif normalized == "autocalculatable":
        target["is_autocalculatable"] = True
    elif normalized == "type":
        target["data_type"] = {
            "alpha": "alpha",
            "choice": "choice",
            "object-list": "object-list",
            "external-list": "external-list",
            "node": "node",
            "integer": "integer-number",
            "real": "real",
        }.get(value.strip().lower(), "unspecified")
    elif normalized == "retaincase":
        target["retains_case"] = True
    elif normalized == "key":
        target["choices"].append(value)
    elif normalized == "object-list":
        target["object_lists"].append(value)
    elif normalized == "external-list":
        target["external_list"] = optional(value)
    elif normalized == "reference":
        target["references"].append(value)
    elif normalized == "reference-class-name":
        target["reference_class_names"].append(value)
    else:
        add_directive(target["additional_directives"], name, value)


def finish_field(target_object: dict[str, Any], field: dict[str, Any] | None) -> None:
    if field is None:
        return
    field["name"] = field["name"].strip() or field["token"]
    if field["data_type"] == "unspecified":
        field["data_type"] = "real" if field["kind"] == "numeric" else "alpha"
    target_object["fields"].append(field)


def finish_object(target: dict[str, Any] | None, objects: list[dict[str, Any]]) -> None:
    if target is None:
        return
    marked = next(
        (field["position"] for field in target["fields"] if field["begins_extensible"]),
        None,
    )
    if marked is not None:
        target["extensible_start_index"] = marked
        canonical_end = marked + target["extensible_group_size"]
        if target["extensible_group_size"] > 0 and len(target["fields"]) > canonical_end:
            target["fields"] = target["fields"][:canonical_end]
    elif target["extensible_group_size"] > 0 and len(target["fields"]) >= target["extensible_group_size"]:
        target["extensible_start_index"] = len(target["fields"]) - target["extensible_group_size"]
    objects.append(target)


def parse_idd(text: str) -> tuple[str, str, list[dict[str, Any]]]:
    version = ""
    build = ""
    group = ""
    objects: list[dict[str, Any]] = []
    current_object: dict[str, Any] | None = None
    current_field: dict[str, Any] | None = None

    for line_number, original_line in enumerate(text.splitlines(), start=1):
        version_match = VERSION_PATTERN.match(original_line)
        if version_match:
            version = version_match.group("value").strip()
            continue
        build_match = BUILD_PATTERN.match(original_line)
        if build_match:
            build = build_match.group("value").strip()
            continue

        line = original_line.split("!", 1)[0]
        trimmed = line.strip()
        if not trimmed or trimmed.startswith("!"):
            continue

        field_match = FIELD_PATTERN.match(line)
        if field_match:
            if current_object is None:
                raise ValueError(f"IDD field found before an object at line {line_number}.")
            for token_match in FIELD_TOKEN_PATTERN.finditer(field_match.group("fields")):
                finish_field(current_object, current_field)
                current_field = new_field(
                    token_match.group("token"),
                    len(current_object["fields"]),
                )
            directives = field_match.group("directives")
            slash = directives.find("\\")
            if slash >= 0:
                name, value = parse_directive(directives[slash:].strip(), line_number)
                apply_field_directive(current_field, name, value)
            continue

        if trimmed.startswith("\\"):
            name, value = parse_directive(trimmed, line_number)
            if name.lower() == "group":
                if current_object is not None:
                    finish_field(current_object, current_field)
                    finish_object(current_object, objects)
                current_object = None
                current_field = None
                group = value
                continue
            if current_object is None:
                continue
            if current_field is None:
                apply_object_directive(current_object, name, value)
            else:
                apply_field_directive(current_field, name, value)
            continue

        if len(trimmed) >= 2 and trimmed.endswith(",") and not trimmed.startswith(("!", "\\")):
            if current_object is not None:
                finish_field(current_object, current_field)
                finish_object(current_object, objects)
            current_object = new_object(trimmed[:-1].strip(), group, len(objects))
            current_field = None
            continue

        raise ValueError(f"Unrecognized IDD syntax at line {line_number}: {trimmed}")

    if current_object is not None:
        finish_field(current_object, current_field)
        finish_object(current_object, objects)
    return version, build, objects


def official_scalar_text(value: Any) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    return str(value)


def schema_types(field_schema: dict[str, Any]) -> set[str]:
    result: set[str] = set()
    direct = field_schema.get("type")
    if isinstance(direct, str):
        result.add(direct)
    for alternative in field_schema.get("anyOf", []):
        alternative_type = alternative.get("type")
        if isinstance(alternative_type, str):
            result.add(alternative_type)
    return result


def schema_enum_values(field_schema: dict[str, Any]) -> list[Any]:
    values: list[Any] = []
    direct = field_schema.get("enum")
    if isinstance(direct, list):
        values.extend(direct)
    for alternative in field_schema.get("anyOf", []):
        nested = alternative.get("enum")
        if isinstance(nested, list):
            values.extend(nested)
    return values


def numeric_schema(field_schema: dict[str, Any]) -> dict[str, Any] | None:
    if field_schema.get("type") in ("number", "integer"):
        return field_schema
    for alternative in field_schema.get("anyOf", []):
        if alternative.get("type") in ("number", "integer"):
            return alternative
    return None


def optional_schema_text(field_schema: dict[str, Any], name: str) -> str | None:
    value = field_schema.get(name)
    return optional(value) if isinstance(value, str) else None


def compare_default(
    errors: list[str],
    context: str,
    actual: str | None,
    field_schema: dict[str, Any],
) -> None:
    if "default" not in field_schema:
        if actual is not None:
            errors.append(f"{context}: IDD default {actual!r} is absent from the official schema.")
        return

    expected = field_schema["default"]
    if actual is None:
        errors.append(f"{context}: official default {expected!r} is absent from the IDD parse.")
        return
    if isinstance(expected, (int, float)) and not isinstance(expected, bool):
        try:
            matches = float(actual) == float(expected)
        except ValueError:
            matches = False
    else:
        expected_text = official_scalar_text(expected)
        if expected_text.casefold() in ("autosize", "autocalculate"):
            matches = actual.casefold() == expected_text.casefold()
        else:
            matches = actual == expected_text
    if not matches:
        errors.append(f"{context}: default differs (IDD {actual!r}, official {expected!r}).")


def compare_bound(
    errors: list[str],
    context: str,
    actual: dict[str, Any] | None,
    field_schema: dict[str, Any] | None,
    inclusive_name: str,
    exclusive_name: str,
) -> None:
    expected: dict[str, Any] | None = None
    if field_schema is not None and inclusive_name in field_schema:
        expected = {"value": float(field_schema[inclusive_name]), "is_inclusive": True}
    elif field_schema is not None and exclusive_name in field_schema:
        expected = {"value": float(field_schema[exclusive_name]), "is_inclusive": False}
    if actual != expected:
        errors.append(f"{context}: numeric bound differs (IDD {actual!r}, official {expected!r}).")


def compare_field_semantics(
    errors: list[str],
    context: str,
    field_key: str,
    actual: dict[str, Any],
    field_info: dict[str, Any],
    field_schema: dict[str, Any],
    is_required: bool,
    token_to_field_key: dict[str, str],
    *,
    compare_name: bool,
) -> tuple[int, int, int, int, int]:
    expected_kind = {"a": "alpha", "n": "numeric"}.get(str(field_info.get("field_type", "")).lower())
    if actual["kind"] != expected_kind:
        errors.append(f"{context}: field kind differs (IDD {actual['kind']!r}, official {expected_kind!r}).")
    if compare_name and actual["name"] != field_info.get("field_name"):
        errors.append(
            f"{context}: field name differs (IDD {actual['name']!r}, "
            f"official {field_info.get('field_name')!r})."
        )
    unrepresented_required_flag = 0
    if is_required and not actual["is_required"]:
        errors.append(
            f"{context}: required flag differs (IDD {actual['is_required']!r}, official {is_required!r})."
        )
    elif actual["is_required"] and not is_required:
        unrepresented_required_flag = 1

    types = schema_types(field_schema)
    data_type = actual["data_type"]
    unrepresented_node = 0
    unrepresented_choice = 0
    if data_type == "object-list":
        matches_type = field_schema.get("data_type") == "object_list"
    elif data_type == "external-list":
        matches_type = field_schema.get("data_type") == "external_list"
    elif data_type == "integer-number":
        matches_type = "integer" in types
    elif data_type == "real":
        matches_type = "number" in types
    elif data_type == "alpha":
        matches_type = "string" in types
    elif data_type == "node":
        matches_type = "string" in types
        unrepresented_node = 1
    elif data_type == "choice":
        matches_type = bool(schema_enum_values(field_schema))
        if not matches_type:
            matches_type = "string" in types or "number" in types
            unrepresented_choice = 1
    else:
        matches_type = False
    if not matches_type:
        errors.append(
            f"{context}: data type {data_type!r} is incompatible with official schema "
            f"types {sorted(types)!r} and data_type {field_schema.get('data_type')!r}."
        )

    official_external_lists = set(field_schema.get("external_list", []))
    if data_type == "external-list" and actual["external_list"] not in official_external_lists:
        errors.append(
            f"{context}: external-list registry differs "
            f"(IDD {actual['external_list']!r}, official {sorted(official_external_lists)!r})."
        )

    compare_default(errors, context, actual["default_value"], field_schema)
    numeric = numeric_schema(field_schema)
    compare_bound(errors, context, actual["minimum"], numeric, "minimum", "exclusiveMinimum")
    compare_bound(errors, context, actual["maximum"], numeric, "maximum", "exclusiveMaximum")

    comparisons = (
        ("units", optional_schema_text(field_schema, "units")),
        ("ip_units", optional_schema_text(field_schema, "ip-units")),
        ("retains_case", bool(field_schema.get("retaincase", False))),
    )
    for property_name, expected in comparisons:
        if actual[property_name] != expected:
            errors.append(
                f"{context}: {property_name} differs (IDD {actual[property_name]!r}, official {expected!r})."
            )

    expected_units_field = optional_schema_text(field_schema, "unitsBasedOnField")
    actual_units_field = actual["units_based_on_field"]
    normalized_units_field = (
        token_to_field_key.get(actual_units_field.upper(), actual_units_field)
        if actual_units_field is not None
        else None
    )
    if expected_units_field not in (actual_units_field, normalized_units_field):
        errors.append(
            f"{context}: units_based_on_field differs "
            f"(IDD {actual_units_field!r}/{normalized_units_field!r}, official {expected_units_field!r})."
        )

    for property_name, official_name in (
        ("object_lists", "object_list"),
        ("references", "reference"),
        ("reference_class_names", "reference-class-name"),
    ):
        expected_values = set(field_schema.get(official_name, []))
        actual_values = set(actual[property_name])
        if actual_values != expected_values:
            errors.append(
                f"{context}: {property_name} differs "
                f"(IDD {sorted(actual_values)!r}, official {sorted(expected_values)!r})."
            )

    enum_text = {
        official_scalar_text(value)
        for value in schema_enum_values(field_schema)
        if official_scalar_text(value) not in ("", "Autosize", "Autocalculate")
    }
    actual_choices = set(actual["choices"])
    official_enum_superset = 0
    if actual_choices and enum_text:
        enum_matches = (
            actual_choices.issubset(enum_text)
            if field_key == "output_unit_type"
            else actual_choices == enum_text
        )
        if not enum_matches:
            errors.append(
                f"{context}: choices differ (IDD {sorted(actual_choices)!r}, official {sorted(enum_text)!r})."
            )
        elif actual_choices != enum_text:
            official_enum_superset = 1

    keyword_values = {
        value for value in schema_enum_values(field_schema) if isinstance(value, str)
    }
    if actual["is_autosizable"] != ("Autosize" in keyword_values):
        errors.append(f"{context}: autosizable flag differs from the official schema.")
    if actual["is_autocalculatable"] != ("Autocalculate" in keyword_values):
        errors.append(f"{context}: autocalculatable flag differs from the official schema.")
    unrepresented_external_list_name = int(
        data_type == "external-list" and not official_external_lists
    )
    return (
        unrepresented_node,
        unrepresented_choice,
        unrepresented_required_flag,
        unrepresented_external_list_name,
        official_enum_superset,
    )


def validate_official_epjson_schema(
    objects: list[dict[str, Any]],
    schema: dict[str, Any],
    expected_version: str,
) -> dict[str, Any]:
    errors: list[str] = []
    if schema.get("$schema") != EPJSON_SCHEMA_DRAFT:
        errors.append(
            f"official schema draft differs (found {schema.get('$schema')!r}, expected {EPJSON_SCHEMA_DRAFT!r})."
        )
    official_objects = schema.get("properties")
    if not isinstance(official_objects, dict):
        raise ValueError("The official epJSON schema does not contain an object-valued 'properties' member.")
    if len(official_objects) != len(objects):
        errors.append(
            f"object count differs (IDD {len(objects)}, official schema {len(official_objects)})."
        )

    required_objects = set(schema.get("required", []))
    field_definition_count = 0
    field_occurrence_count = 0
    extensible_object_count = 0
    extensible_prototype_count = 0
    unrepresented_node_type_count = 0
    unrepresented_choice_type_count = 0
    unrepresented_required_flag_count = 0
    unrepresented_external_list_name_count = 0
    unrepresented_field_topology_object_count = 0
    official_enum_superset_field_count = 0

    for object_index, ((official_name, official), actual) in enumerate(zip(official_objects.items(), objects)):
        context = f"object[{object_index}] {official_name}"
        if actual["name"] != official_name:
            errors.append(f"{context}: name differs (IDD {actual['name']!r}).")
        if actual["group"] != official.get("group", ""):
            errors.append(
                f"{context}: group differs (IDD {actual['group']!r}, official {official.get('group', '')!r})."
            )
        expected_unique = official.get("maxProperties") == 1
        expected_required = official_name in required_objects
        expected_minimum_fields = int(official.get("min_fields", 0))
        expected_extensible_size = int(official.get("extensible_size", 0))
        expected_format = optional_schema_text(official, "format")
        for property_name, expected in (
            ("is_unique", expected_unique),
            ("is_required", expected_required),
            ("minimum_fields", expected_minimum_fields),
            ("extensible_group_size", expected_extensible_size),
            ("format", expected_format),
        ):
            if actual[property_name] != expected:
                errors.append(
                    f"{context}: {property_name} differs (IDD {actual[property_name]!r}, official {expected!r})."
                )

        legacy = official.get("legacy_idd")
        pattern_properties = official.get("patternProperties")
        if not isinstance(legacy, dict) or not isinstance(pattern_properties, dict) or len(pattern_properties) != 1:
            errors.append(f"{context}: unsupported official legacy_idd/patternProperties topology.")
            continue
        instance = next(iter(pattern_properties.values()))
        instance_properties = instance.get("properties", {})
        fixed_names = list(legacy.get("fields", []))
        extensible_names = list(legacy.get("extensibles", []))
        field_info = legacy.get("field_info", {})
        fixed_required = set(instance.get("required", []))
        actual_fields = actual["fields"]
        token_to_field_key: dict[str, str] = {}
        for field_index, field_key in enumerate(fixed_names):
            if field_index < len(actual_fields):
                token_to_field_key[actual_fields[field_index]["token"].upper()] = field_key
        expected_start_for_tokens = len(fixed_names)
        for offset, field_key in enumerate(extensible_names):
            field_index = expected_start_for_tokens + offset
            if field_index < len(actual_fields):
                token_to_field_key[actual_fields[field_index]["token"].upper()] = field_key
        field_definition_count += len(fixed_names) + len(extensible_names)
        field_occurrence_count += len(fixed_names) + len(extensible_names)
        if expected_extensible_size > 0:
            extensible_object_count += 1

        for field_index, field_key in enumerate(fixed_names):
            if field_index >= len(actual_fields):
                errors.append(f"{context}: fixed field {field_key!r} is absent from the IDD parse.")
                continue
            field_schema = (
                official["name"]
                if field_key == "name" and "name" in official
                else instance_properties.get(field_key)
            )
            if not isinstance(field_schema, dict) or field_key not in field_info:
                errors.append(f"{context}: official fixed field {field_key!r} cannot be resolved.")
                continue
            required = field_key in fixed_required or bool(field_schema.get("is_required", False))
            (
                node_count,
                choice_count,
                required_flag_count,
                external_list_name_count,
                enum_superset_count,
            ) = compare_field_semantics(
                errors,
                f"{context} field[{field_index}] {field_key}",
                field_key,
                actual_fields[field_index],
                field_info[field_key],
                field_schema,
                required,
                token_to_field_key,
                compare_name=True,
            )
            unrepresented_node_type_count += node_count
            unrepresented_choice_type_count += choice_count
            unrepresented_required_flag_count += required_flag_count
            unrepresented_external_list_name_count += external_list_name_count
            official_enum_superset_field_count += enum_superset_count

        if extensible_names:
            extensible_prototype_count += len(extensible_names)
            expected_start = len(fixed_names)
            if actual["extensible_start_index"] != expected_start:
                errors.append(
                    f"{context}: extensible start differs (IDD {actual['extensible_start_index']!r}, "
                    f"official {expected_start!r})."
                )
            if len(extensible_names) != expected_extensible_size:
                errors.append(
                    f"{context}: official extensible prototype count does not match extensible_size."
                )
            remaining = len(actual_fields) - expected_start
            if remaining < expected_extensible_size or remaining % expected_extensible_size != 0:
                errors.append(
                    f"{context}: expanded IDD field count does not contain complete extensible groups."
                )
            extension_name = legacy.get("extension")
            extension_schema = instance_properties.get(extension_name, {})
            item_schema = extension_schema.get("items", {}) if isinstance(extension_schema, dict) else {}
            extension_properties = item_schema.get("properties", {})
            extension_required = set(item_schema.get("required", []))
            for offset, field_key in enumerate(extensible_names):
                field_index = expected_start + offset
                field_schema = extension_properties.get(field_key)
                if field_index >= len(actual_fields) or not isinstance(field_schema, dict) or field_key not in field_info:
                    errors.append(f"{context}: official extensible field {field_key!r} cannot be resolved.")
                    continue
                required = field_key in extension_required or bool(field_schema.get("is_required", False))
                (
                    node_count,
                    choice_count,
                    required_flag_count,
                    external_list_name_count,
                    enum_superset_count,
                ) = compare_field_semantics(
                    errors,
                    f"{context} extensible[{offset}] {field_key}",
                    field_key,
                    actual_fields[field_index],
                    field_info[field_key],
                    field_schema,
                    required,
                    token_to_field_key,
                    compare_name=False,
                )
                unrepresented_node_type_count += node_count
                unrepresented_choice_type_count += choice_count
                unrepresented_required_flag_count += required_flag_count
                unrepresented_external_list_name_count += external_list_name_count
                official_enum_superset_field_count += enum_superset_count
            for field_index in range(expected_start, len(actual_fields)):
                field_key = extensible_names[(field_index - expected_start) % len(extensible_names)]
                expected_kind = {
                    "a": "alpha",
                    "n": "numeric",
                }.get(str(field_info[field_key].get("field_type", "")).lower())
                if actual_fields[field_index]["kind"] != expected_kind:
                    errors.append(
                        f"{context} expanded field[{field_index}]: kind differs from extensible prototype "
                        f"{field_key!r}."
                    )
        else:
            if not fixed_names and actual_fields:
                unrepresented_field_topology_object_count += 1
            elif len(actual_fields) != len(fixed_names):
                errors.append(
                    f"{context}: field count differs (IDD {len(actual_fields)}, official {len(fixed_names)})."
                )
            expected_start = (
                len(actual_fields) - expected_extensible_size
                if expected_extensible_size > 0 and len(actual_fields) >= expected_extensible_size
                else None
            )
            if actual["extensible_start_index"] != expected_start:
                errors.append(
                    f"{context}: fallback extensible start differs "
                    f"(IDD {actual['extensible_start_index']!r}, official {expected_start!r})."
                )

    version_object = official_objects.get("Version", {})
    version_instance = next(iter(version_object.get("patternProperties", {}).values()), {})
    official_version = version_instance.get("properties", {}).get("version_identifier", {}).get("default")
    expected_major_minor = ".".join(expected_version.split(".")[:2])
    if official_scalar_text(official_version) != expected_major_minor:
        errors.append(
            f"official Version default differs (found {official_version!r}, expected {expected_major_minor!r})."
        )

    if errors:
        displayed = "\n - ".join(errors[:50])
        remainder = "" if len(errors) <= 50 else f"\n - ... and {len(errors) - 50} more"
        raise ValueError(
            f"Official epJSON semantic validation found {len(errors)} differences:\n - {displayed}{remainder}"
        )

    return {
        "schema_draft": EPJSON_SCHEMA_DRAFT,
        "energyplus_version": official_scalar_text(official_version),
        "object_count": len(official_objects),
        "field_definition_count": field_definition_count,
        "validated_field_occurrence_count": field_occurrence_count,
        "extensible_object_count": extensible_object_count,
        "extensible_prototype_field_count": extensible_prototype_count,
        "unrepresented_node_type_count": unrepresented_node_type_count,
        "unrepresented_choice_type_count": unrepresented_choice_type_count,
        "unrepresented_required_flag_count": unrepresented_required_flag_count,
        "unrepresented_external_list_name_count": unrepresented_external_list_name_count,
        "unrepresented_field_topology_object_count": unrepresented_field_topology_object_count,
        "official_enum_superset_field_count": official_enum_superset_field_count,
        "validated_dimensions": [
            "object-name-order-group-unique-required-min-fields-format",
            "field-order-name-kind-required-default-enum",
            "field-type-units-bounds-autosize-autocalculate",
            "field-object-list-external-list-reference-reference-class",
            "extensible-start-size-prototype-kind-cycle",
        ],
        "not_compared_metadata": [
            "IDD token identifiers and comma/semicolon delimiters",
            "line segmentation of memo and note directives",
            "begin-extensible marker spelling",
            "unknown or additional raw IDD directives",
            "node versus unconstrained alpha type (not distinguished by official epJSON)",
            "legacy choice type markers whose keys are not retained by official epJSON",
            "raw required-field flags omitted from official epJSON validation constraints (often represented as optional-plus-default)",
            "additional output-unit enum members introduced by official epJSON's shared unit-type schema",
            "legacy fields of six ground-heat-transfer face objects omitted by official epJSON",
        ],
    }


def write_deterministic_gzip_json(path: Path, value: Any) -> None:
    payload = (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("wb") as destination:
        with gzip.GzipFile(filename="", mode="wb", fileobj=destination, mtime=0) as compressed:
            compressed.write(payload)


def main() -> None:
    args = parse_arguments()
    if sys.version_info[:3] != REQUIRED_PYTHON:
        raise SystemExit(
            f"IDD oracle generation requires Python {'.'.join(map(str, REQUIRED_PYTHON))}; "
            f"found {'.'.join(map(str, sys.version_info[:3]))}."
        )
    if os.environ.get("PYTHONHASHSEED") != "0":
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic IDD oracle generation.")

    raw = args.idd.resolve().read_bytes()
    source_hash = sha256_bytes(raw)
    expected_hash = args.expected_sha256.strip().lower()
    if source_hash != expected_hash:
        raise SystemExit(f"IDD hash mismatch: expected {expected_hash}, found {source_hash}.")
    text = raw.decode("utf-8-sig")
    version, build, objects = parse_idd(text)
    if version != args.expected_version:
        raise SystemExit(f"IDD version mismatch: expected {args.expected_version}, found {version}.")
    if build != args.expected_build:
        raise SystemExit(f"IDD build mismatch: expected {args.expected_build}, found {build}.")

    epjson_raw = args.epjson_schema.resolve().read_bytes()
    epjson_source_hash = sha256_bytes(epjson_raw)
    expected_epjson_hash = args.expected_epjson_sha256.strip().lower()
    if epjson_source_hash != expected_epjson_hash:
        raise SystemExit(
            f"Official epJSON schema hash mismatch: expected {expected_epjson_hash}, "
            f"found {epjson_source_hash}."
        )
    try:
        epjson_schema = json.loads(epjson_raw.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exception:
        raise SystemExit(f"Official epJSON schema is not valid UTF-8 JSON: {exception}") from exception
    official_validation = validate_official_epjson_schema(objects, epjson_schema, version)
    official_validation.update(
        {
            "source_sha256": epjson_source_hash,
            "source_bytes": len(epjson_raw),
            "paired_energyplus_build": build,
        }
    )

    groups: list[str] = []
    seen_groups: set[str] = set()
    for item in objects:
        normalized = item["group"].casefold()
        if normalized not in seen_groups:
            seen_groups.add(normalized)
            groups.append(item["group"])

    field_count = sum(len(item["fields"]) for item in objects)
    oracle = {
        "oracle_schema": ORACLE_SCHEMA,
        "upstream_commit": args.upstream_commit,
        "energyplus_version": version,
        "energyplus_build": build,
        "source_sha256": source_hash,
        "source_bytes": len(raw),
        "object_count": len(objects),
        "field_count": field_count,
        "groups": groups,
        "objects": objects,
        "official_epjson_schema": official_validation,
    }
    write_deterministic_gzip_json(args.output.resolve(), oracle)
    print(
        f"Generated full IDD oracle with {len(objects)} objects and {field_count} fields: "
        f"{args.output.resolve()}"
    )


if __name__ == "__main__":
    main()

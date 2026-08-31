"""Build the PDF-only Dragon Grasshopper user guide with OODocs.

The component reference is generated from two deliberately separate sources:

* the runtime catalog records exactly what Grasshopper exposes; and
* ``component-guides.json`` supplies the practical explanation that cannot be
  recovered reliably from component metadata alone.

Both sources are validated before any output is replaced.  Component GUIDs are
accepted only as catalog integrity data and are discarded before the public
document model is created.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass, replace
import json
import os
from pathlib import Path
import re
import sys
import tempfile
from types import SimpleNamespace
from typing import Any, Mapping, Sequence


CATALOG_SCHEMA = "goniegonie.dragons.component-catalog.v1"
GUIDE_SCHEMA = "goniegonie.dragons.component-guides.v1"
EXPECTED_OODOCS_VERSION = "1.3.0"
EXPECTED_COMPONENT_COUNT = 75
EXPECTED_PRODUCTS = ("InvisibleDragon", "SimpleDragon")

GUIDES_PATH = Path("tools/documentation/component-guides.json")
REFERENCE_PATH = Path("docs/user-guide/02-in-out-reference.md")
SOURCE_CHAPTERS = (
    ("01-workflow.md", "Workflow"),
    ("02-in-out-reference.md", "Component In/Out Reference"),
    ("03-compatibility.md", "Compatibility"),
    ("04-release-notes.md", "Release Notes"),
)


class UserGuideBuildError(RuntimeError):
    """Raised when source data cannot produce a trustworthy user guide."""


@dataclass(frozen=True, slots=True)
class PortChoice:
    """One exact selectable value exposed by a Grasshopper input."""

    value: str
    label: str


@dataclass(frozen=True, slots=True)
class ComponentPort:
    """One runtime-discovered Grasshopper input or output port."""

    index: int
    name: str
    nickname: str
    description: str
    friendly_type: str
    runtime_type: str
    access: str
    optional: bool | None
    has_persistent_default: bool
    default_values: tuple[str, ...]
    choices: tuple[PortChoice, ...]


@dataclass(frozen=True, slots=True)
class RuntimeComponent:
    """Public runtime metadata for one Grasshopper component.

    The internal component GUID is intentionally not a field on this public
    documentation model.
    """

    product: str
    runtime_type: str
    name: str
    nickname: str
    description: str
    category: str
    subcategory: str
    exposure: str
    inputs: tuple[ComponentPort, ...]
    outputs: tuple[ComponentPort, ...]


@dataclass(frozen=True, slots=True)
class TypedParameter:
    """One standalone typed Grasshopper parameter discovered at runtime."""

    product: str
    runtime_type: str
    name: str
    nickname: str
    description: str
    friendly_type: str
    category: str
    subcategory: str
    exposure: str


@dataclass(frozen=True, slots=True)
class ProductCatalog:
    """Runtime components and typed parameters belonging to one product."""

    product: str
    components: tuple[RuntimeComponent, ...]
    parameters: tuple[TypedParameter, ...]


@dataclass(frozen=True, slots=True)
class RuntimeCatalog:
    """Validated runtime catalog used to author the public reference."""

    schema: str
    framework: str
    component_count: int
    parameter_count: int
    products: tuple[ProductCatalog, ...]

    @property
    def components(self) -> tuple[RuntimeComponent, ...]:
        return tuple(
            component
            for product in self.products
            for component in product.components
        )

    @property
    def parameters(self) -> tuple[TypedParameter, ...]:
        return tuple(
            parameter
            for product in self.products
            for parameter in product.parameters
        )


@dataclass(frozen=True, slots=True)
class ComponentGuide:
    """Curated, practical guidance for one runtime component."""

    role: str
    purpose: str
    workflow: str
    caveats: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class DetailedGuideCatalog:
    """Curated guidance keyed exactly by component runtime type."""

    schema: str
    components: Mapping[str, ComponentGuide]


@dataclass(frozen=True, slots=True)
class BuildSummary:
    """Machine-readable facts emitted after a successful build."""

    component_count: int
    parameter_count: int
    reference_updated: bool
    reference_path: Path
    pdf_path: Path


def _object(value: Any, location: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise UserGuideBuildError(f"{location} must be a JSON object.")
    if not all(isinstance(key, str) for key in value):
        raise UserGuideBuildError(f"{location} has a non-string JSON key.")
    return value


def _array(value: Any, location: str) -> list[Any]:
    if not isinstance(value, list):
        raise UserGuideBuildError(f"{location} must be a JSON array.")
    return value


def _text(value: Any, location: str, *, allow_empty: bool = False) -> str:
    if not isinstance(value, str):
        raise UserGuideBuildError(f"{location} must be text.")
    if not allow_empty and not value.strip():
        raise UserGuideBuildError(f"{location} must not be blank.")
    return value


def _integer(value: Any, location: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        raise UserGuideBuildError(f"{location} must be an integer.")
    return value


def _boolean(value: Any, location: str) -> bool:
    if not isinstance(value, bool):
        raise UserGuideBuildError(f"{location} must be true or false.")
    return value


def _exact_keys(value: Mapping[str, Any], expected: set[str], location: str) -> None:
    actual = set(value)
    missing = sorted(expected - actual)
    extra = sorted(actual - expected)
    if missing or extra:
        raise UserGuideBuildError(
            f"{location} has an unexpected shape; missing={missing}, extra={extra}."
        )


def _text_array(value: Any, location: str, *, require_nonempty: bool = False) -> tuple[str, ...]:
    items = _array(value, location)
    if require_nonempty and not items:
        raise UserGuideBuildError(f"{location} must contain at least one text item.")
    return tuple(
        _text(item, f"{location}[{index}]", allow_empty=not require_nonempty)
        for index, item in enumerate(items)
    )


def _load_json(path: Path, description: str) -> dict[str, Any]:
    if not path.is_file():
        raise UserGuideBuildError(f"{description} is missing: {path}")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except UnicodeDecodeError as exc:
        raise UserGuideBuildError(f"{description} is not valid UTF-8: {path}") from exc
    except json.JSONDecodeError as exc:
        raise UserGuideBuildError(
            f"{description} is not valid JSON at line {exc.lineno}, column {exc.colno}: {path}"
        ) from exc
    return _object(value, description)


def _parse_choice(value: Any, location: str) -> PortChoice:
    data = _object(value, location)
    _exact_keys(data, {"value", "label"}, location)
    return PortChoice(
        value=_text(data["value"], f"{location}.value"),
        label=_text(data["label"], f"{location}.label"),
    )


def _parse_port(value: Any, location: str, *, is_input: bool) -> ComponentPort:
    data = _object(value, location)
    _exact_keys(
        data,
        {
            "index",
            "name",
            "nickname",
            "description",
            "friendlyType",
            "runtimeType",
            "access",
            "optional",
            "hasPersistentDefault",
            "defaultValues",
            "choices",
        },
        location,
    )
    optional_value = data["optional"]
    if is_input:
        optional = _boolean(optional_value, f"{location}.optional")
    else:
        if optional_value is not None:
            raise UserGuideBuildError(f"{location}.optional must be null for an output.")
        optional = None

    default_values = _text_array(data["defaultValues"], f"{location}.defaultValues")
    has_default = _boolean(
        data["hasPersistentDefault"],
        f"{location}.hasPersistentDefault",
    )
    if has_default != bool(default_values):
        raise UserGuideBuildError(
            f"{location} disagrees about whether persistent defaults are present."
        )

    choice_items = _array(data["choices"], f"{location}.choices")
    choices = tuple(
        _parse_choice(item, f"{location}.choices[{index}]")
        for index, item in enumerate(choice_items)
    )
    choice_values = [choice.value for choice in choices]
    if len(choice_values) != len(set(choice_values)):
        raise UserGuideBuildError(f"{location}.choices contains duplicate values.")

    access = _text(data["access"], f"{location}.access")
    if access not in {"item", "list", "tree"}:
        raise UserGuideBuildError(
            f"{location}.access must be item, list, or tree; found {access!r}."
        )
    index = _integer(data["index"], f"{location}.index")
    if index < 0:
        raise UserGuideBuildError(f"{location}.index must be non-negative.")

    return ComponentPort(
        index=index,
        name=_text(data["name"], f"{location}.name"),
        nickname=_text(data["nickname"], f"{location}.nickname"),
        description=_text(data["description"], f"{location}.description"),
        friendly_type=_text(data["friendlyType"], f"{location}.friendlyType"),
        runtime_type=_text(data["runtimeType"], f"{location}.runtimeType"),
        access=access,
        optional=optional,
        has_persistent_default=has_default,
        default_values=default_values,
        choices=choices,
    )


def _parse_ports(value: Any, location: str, *, is_input: bool) -> tuple[ComponentPort, ...]:
    items = _array(value, location)
    ports = tuple(
        _parse_port(item, f"{location}[{index}]", is_input=is_input)
        for index, item in enumerate(items)
    )
    actual_indexes = tuple(port.index for port in ports)
    expected_indexes = tuple(range(len(ports)))
    if actual_indexes != expected_indexes:
        raise UserGuideBuildError(
            f"{location} indexes must be contiguous and ordered; "
            f"expected={expected_indexes}, actual={actual_indexes}."
        )
    return ports


def _parse_component(
    value: Any,
    location: str,
    *,
    expected_product: str,
) -> tuple[RuntimeComponent, str]:
    data = _object(value, location)
    _exact_keys(
        data,
        {
            "product",
            "runtimeType",
            "guid",
            "name",
            "nickname",
            "description",
            "category",
            "subcategory",
            "exposure",
            "inputs",
            "outputs",
        },
        location,
    )
    product = _text(data["product"], f"{location}.product")
    if product != expected_product:
        raise UserGuideBuildError(
            f"{location}.product is {product!r}, expected {expected_product!r}."
        )
    internal_guid = _text(data["guid"], f"{location}.guid")
    component = RuntimeComponent(
        product=product,
        runtime_type=_text(data["runtimeType"], f"{location}.runtimeType"),
        name=_text(data["name"], f"{location}.name"),
        nickname=_text(data["nickname"], f"{location}.nickname"),
        description=_text(data["description"], f"{location}.description"),
        category=_text(data["category"], f"{location}.category"),
        subcategory=_text(data["subcategory"], f"{location}.subcategory"),
        exposure=_text(data["exposure"], f"{location}.exposure"),
        inputs=_parse_ports(data["inputs"], f"{location}.inputs", is_input=True),
        outputs=_parse_ports(data["outputs"], f"{location}.outputs", is_input=False),
    )
    return component, internal_guid


def _parse_parameter(
    value: Any,
    location: str,
    *,
    expected_product: str,
) -> tuple[TypedParameter, str]:
    data = _object(value, location)
    _exact_keys(
        data,
        {
            "product",
            "runtimeType",
            "guid",
            "name",
            "nickname",
            "description",
            "friendlyType",
            "category",
            "subcategory",
            "exposure",
        },
        location,
    )
    product = _text(data["product"], f"{location}.product")
    if product != expected_product:
        raise UserGuideBuildError(
            f"{location}.product is {product!r}, expected {expected_product!r}."
        )
    internal_guid = _text(data["guid"], f"{location}.guid")
    parameter = TypedParameter(
        product=product,
        runtime_type=_text(data["runtimeType"], f"{location}.runtimeType"),
        name=_text(data["name"], f"{location}.name"),
        nickname=_text(data["nickname"], f"{location}.nickname"),
        description=_text(data["description"], f"{location}.description"),
        friendly_type=_text(data["friendlyType"], f"{location}.friendlyType"),
        category=_text(data["category"], f"{location}.category"),
        subcategory=_text(data["subcategory"], f"{location}.subcategory"),
        exposure=_text(data["exposure"], f"{location}.exposure"),
    )
    return parameter, internal_guid


def load_runtime_catalog(path: Path) -> tuple[RuntimeCatalog, Mapping[str, str]]:
    """Load and strictly validate the runtime catalog.

    The returned runtime-type-to-GUID mapping is used only to compare saved
    object identity across hosts and prove that GUIDs do not leak into public
    sources or outputs.
    """

    data = _load_json(path, "Runtime component catalog")
    _exact_keys(
        data,
        {"schema", "framework", "componentCount", "parameterCount", "products"},
        "Runtime component catalog",
    )
    schema = _text(data["schema"], "Runtime component catalog.schema")
    if schema != CATALOG_SCHEMA:
        raise UserGuideBuildError(
            f"Unsupported runtime catalog schema {schema!r}; expected {CATALOG_SCHEMA!r}."
        )

    product_values = _array(data["products"], "Runtime component catalog.products")
    products: list[ProductCatalog] = []
    internal_identities: list[tuple[str, str]] = []
    for product_index, product_value in enumerate(product_values):
        location = f"Runtime component catalog.products[{product_index}]"
        product_data = _object(product_value, location)
        _exact_keys(product_data, {"product", "components", "parameters"}, location)
        product_name = _text(product_data["product"], f"{location}.product")

        component_values = _array(product_data["components"], f"{location}.components")
        components: list[RuntimeComponent] = []
        for component_index, component_value in enumerate(component_values):
            component, internal_guid = _parse_component(
                component_value,
                f"{location}.components[{component_index}]",
                expected_product=product_name,
            )
            components.append(component)
            internal_identities.append(
                (f"component:{component.runtime_type}", internal_guid.casefold())
            )

        parameter_values = _array(product_data["parameters"], f"{location}.parameters")
        parameters: list[TypedParameter] = []
        for parameter_index, parameter_value in enumerate(parameter_values):
            parameter, internal_guid = _parse_parameter(
                parameter_value,
                f"{location}.parameters[{parameter_index}]",
                expected_product=product_name,
            )
            parameters.append(parameter)
            internal_identities.append(
                (f"parameter:{parameter.runtime_type}", internal_guid.casefold())
            )

        products.append(
            ProductCatalog(
                product=product_name,
                components=tuple(components),
                parameters=tuple(parameters),
            )
        )

    product_names = tuple(product.product for product in products)
    if set(product_names) != set(EXPECTED_PRODUCTS) or len(product_names) != len(EXPECTED_PRODUCTS):
        raise UserGuideBuildError(
            f"Runtime catalog products must be exactly {EXPECTED_PRODUCTS}; found {product_names}."
        )

    component_count = _integer(
        data["componentCount"],
        "Runtime component catalog.componentCount",
    )
    parameter_count = _integer(
        data["parameterCount"],
        "Runtime component catalog.parameterCount",
    )
    actual_component_count = sum(len(product.components) for product in products)
    actual_parameter_count = sum(len(product.parameters) for product in products)
    if component_count != actual_component_count:
        raise UserGuideBuildError(
            "Runtime catalog componentCount does not match its component records: "
            f"declared={component_count}, actual={actual_component_count}."
        )
    if parameter_count != actual_parameter_count:
        raise UserGuideBuildError(
            "Runtime catalog parameterCount does not match its parameter records: "
            f"declared={parameter_count}, actual={actual_parameter_count}."
        )
    if component_count != EXPECTED_COMPONENT_COUNT:
        raise UserGuideBuildError(
            f"The user guide requires exactly {EXPECTED_COMPONENT_COUNT} runtime components; "
            f"found {component_count}."
        )

    runtime_types = [
        component.runtime_type
        for product in products
        for component in product.components
    ]
    if len(runtime_types) != len(set(runtime_types)):
        raise UserGuideBuildError("Runtime component types are not globally unique.")
    parameter_types = [
        parameter.runtime_type
        for product in products
        for parameter in product.parameters
    ]
    if len(parameter_types) != len(set(parameter_types)):
        raise UserGuideBuildError("Typed-parameter runtime types are not globally unique.")
    identity_keys = [key for key, _ in internal_identities]
    if len(identity_keys) != len(set(identity_keys)):
        raise UserGuideBuildError("Internal catalog identity keys are not globally unique.")
    normalized_guids = [value for _, value in internal_identities]
    if len(normalized_guids) != len(set(normalized_guids)):
        raise UserGuideBuildError("Internal catalog GUIDs are not globally unique.")

    catalog = RuntimeCatalog(
        schema=schema,
        framework=_text(data["framework"], "Runtime component catalog.framework"),
        component_count=component_count,
        parameter_count=parameter_count,
        products=tuple(products),
    )
    return catalog, dict(internal_identities)


def validate_host_catalog_compatibility(
    primary: RuntimeCatalog,
    primary_identities: Mapping[str, str],
    candidate: RuntimeCatalog,
    candidate_identities: Mapping[str, str],
) -> None:
    """Require two Rhino target builds to expose the same public contract.

    ``framework`` is expected to differ. Everything a Grasshopper user can see
    or wire, plus the internal object identities used to keep saved definitions
    loadable, must remain identical across the supported Rhino hosts.
    """

    if candidate.framework == primary.framework:
        raise UserGuideBuildError(
            "Compatibility catalogs must describe distinct frameworks; "
            f"both declare {primary.framework!r}."
        )
    if candidate.schema != primary.schema:
        raise UserGuideBuildError(
            "Rhino host catalogs use different schemas: "
            f"{primary.framework}={primary.schema!r}, "
            f"{candidate.framework}={candidate.schema!r}."
        )
    if candidate.component_count != primary.component_count:
        raise UserGuideBuildError(
            "Rhino host component counts differ: "
            f"{primary.framework}={primary.component_count}, "
            f"{candidate.framework}={candidate.component_count}."
        )
    if candidate.parameter_count != primary.parameter_count:
        raise UserGuideBuildError(
            "Rhino host typed-parameter counts differ: "
            f"{primary.framework}={primary.parameter_count}, "
            f"{candidate.framework}={candidate.parameter_count}."
        )
    if candidate.products != primary.products:
        raise UserGuideBuildError(
            "Rhino host catalogs expose different component, port, choice, "
            "default, or typed-parameter metadata: "
            f"{primary.framework!r} versus {candidate.framework!r}."
        )
    if candidate_identities != primary_identities:
        raise UserGuideBuildError(
            "Rhino host catalogs expose different internal component or "
            "typed-parameter runtime-type-to-GUID identities: "
            f"{primary.framework!r} versus {candidate.framework!r}."
        )


def load_detailed_guides(path: Path) -> DetailedGuideCatalog:
    """Load the hand-authored guidance with strict non-empty text checks."""

    data = _load_json(path, "Detailed component guide catalog")
    _exact_keys(data, {"schema", "components"}, "Detailed component guide catalog")
    schema = _text(data["schema"], "Detailed component guide catalog.schema")
    if schema != GUIDE_SCHEMA:
        raise UserGuideBuildError(
            f"Unsupported guide schema {schema!r}; expected {GUIDE_SCHEMA!r}."
        )
    component_values = _object(
        data["components"],
        "Detailed component guide catalog.components",
    )
    guides: dict[str, ComponentGuide] = {}
    for runtime_type, guide_value in component_values.items():
        runtime_key = _text(runtime_type, "Detailed component guide key")
        location = f"Detailed component guide catalog.components[{runtime_key!r}]"
        guide_data = _object(guide_value, location)
        _exact_keys(guide_data, {"role", "purpose", "workflow", "caveats"}, location)
        guides[runtime_key] = ComponentGuide(
            role=_text(guide_data["role"], f"{location}.role"),
            purpose=_text(guide_data["purpose"], f"{location}.purpose"),
            workflow=_text(guide_data["workflow"], f"{location}.workflow"),
            caveats=_text_array(
                guide_data["caveats"],
                f"{location}.caveats",
                require_nonempty=True,
            ),
        )
    return DetailedGuideCatalog(schema=schema, components=guides)


def validate_guide_coverage(
    catalog: RuntimeCatalog,
    guides: DetailedGuideCatalog,
) -> None:
    """Require an exact one-to-one runtime component/guide key relationship."""

    runtime_keys = {component.runtime_type for component in catalog.components}
    guide_keys = set(guides.components)
    missing = sorted(runtime_keys - guide_keys)
    extra = sorted(guide_keys - runtime_keys)
    if missing or extra:
        raise UserGuideBuildError(
            "Detailed guide keys must match runtime component keys exactly; "
            f"missing={missing}, extra={extra}."
        )
    if len(guides.components) != catalog.component_count:
        raise UserGuideBuildError(
            "Detailed guide count differs from the runtime catalog after key validation."
        )


def _product_order(product: str) -> tuple[int, str]:
    try:
        return EXPECTED_PRODUCTS.index(product), product.casefold()
    except ValueError:
        return len(EXPECTED_PRODUCTS), product.casefold()


def _single_line(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip()


def _table_cell(value: Any) -> str:
    text = _single_line(str(value))
    return text.replace("\\", "\\\\").replace("|", "\\|") or "—"


def _code(value: str) -> str:
    normalized = _single_line(value)
    if not normalized:
        normalized = '""'
    # Consolas is embedded for ordinary code, but it does not contain Hangul.
    # Leave non-WinAnsi values in the Malgun Gothic body font so a Korean
    # address never forces ReportLab's non-embedded HYGothic CID fallback.
    try:
        normalized.encode("cp1252")
    except UnicodeEncodeError:
        return normalized
    delimiter = "``" if "`" in normalized else "`"
    return f"{delimiter}{normalized}{delimiter}"


def _table(headers: Sequence[str], rows: Sequence[Sequence[Any]]) -> list[str]:
    lines = [
        "| " + " | ".join(_table_cell(header) for header in headers) + " |",
        "| " + " | ".join("---" for _ in headers) + " |",
    ]
    for row_index, row in enumerate(rows):
        if len(row) != len(headers):
            raise UserGuideBuildError(
                f"Generated table row {row_index} has {len(row)} cells; expected {len(headers)}."
            )
        lines.append("| " + " | ".join(_table_cell(cell) for cell in row) + " |")
    return lines


def _default_and_choices(port: ComponentPort) -> str:
    details: list[str] = []
    if port.default_values:
        details.append(
            "Default: " + ", ".join(_code(value) for value in port.default_values)
        )
    if port.choices:
        choices = []
        for choice in port.choices:
            if choice.label == choice.value:
                choices.append(_code(choice.value))
            else:
                choices.append(f"{choice.label} ({_code(choice.value)})")
        details.append("Choices: " + "; ".join(choices))
    return "; ".join(details) or "—"


def _component_flags(component: RuntimeComponent, guide: ComponentGuide) -> tuple[str, ...]:
    flags: list[str] = []
    role_key = guide.role.strip().casefold()
    if role_key == "utility":
        flags.append("UTILITY")
    elif role_key == "trigger":
        flags.append("RUN TRIGGER")
    elif role_key == "result":
        flags.append("RESULT / ANALYSIS")
    elif role_key == "choice":
        flags.append("CHOICE")
    if any(port.choices for port in component.inputs):
        flags.append("CHOICE INPUTS")
    return tuple(flags)


def _component_markdown(
    component: RuntimeComponent,
    guide: ComponentGuide,
) -> list[str]:
    lines = [
        f"##### {component.name} ({_code(component.nickname)})",
        "",
        f"**Role:** {_single_line(guide.role)}",
        "",
    ]
    flags = _component_flags(component, guide)
    if flags:
        lines.extend(
            [
                "**Flags:** " + " · ".join(_code(flag) for flag in flags),
                "",
            ]
        )
    lines.extend(
        [
            f"**Purpose:** {_single_line(guide.purpose)}",
            "",
            f"**How to use it:** {_single_line(guide.workflow)}",
            "",
            f"**Canvas location:** {component.category} → {component.subcategory}. "
            f"Exposure: {_code(component.exposure)}.",
            "",
            "**Important caveats:**",
            "",
        ]
    )
    lines.extend(f"- {_single_line(caveat)}" for caveat in guide.caveats)
    lines.extend(["", "**Inputs**", ""])
    if component.inputs:
        input_rows = [
            (
                port.index,
                f"{port.name} ({_code(port.nickname)})",
                port.friendly_type,
                port.access.title(),
                "Yes" if port.optional else "No",
                _default_and_choices(port),
                port.description,
            )
            for port in component.inputs
        ]
        lines.extend(
            _table(
                (
                    "#",
                    "Input (nickname)",
                    "Wire type",
                    "Access",
                    "Optional",
                    "Default / choices",
                    "Description",
                ),
                input_rows,
            )
        )
    else:
        lines.append("_This component has no inputs._")

    lines.extend(["", "**Outputs**", ""])
    if component.outputs:
        output_rows = [
            (
                port.index,
                f"{port.name} ({_code(port.nickname)})",
                port.friendly_type,
                port.access.title(),
                port.description,
            )
            for port in component.outputs
        ]
        lines.extend(
            _table(
                ("#", "Output (nickname)", "Wire type", "Access", "Description"),
                output_rows,
            )
        )
    else:
        lines.append("_This component has no outputs._")
    lines.append("")
    return lines


def render_component_reference(
    catalog: RuntimeCatalog,
    guides: DetailedGuideCatalog,
) -> str:
    """Render the complete deterministic component and parameter reference."""

    validate_guide_coverage(catalog, guides)
    lines = [
        "# Component In/Out Reference",
        "",
        "This reference combines runtime-reflected Grasshopper ports with curated "
        "workflow guidance. It covers every public component in InvisibleDragon and "
        "SimpleDragon; port order, access mode, defaults, choices, and wire types come "
        "from the built plugins rather than a manually maintained list.",
        "",
        f"**Coverage:** {catalog.component_count} components and "
        f"{catalog.parameter_count} standalone typed parameters for "
        f"{_code(catalog.framework)}.",
        "",
        "A port marked optional accepts an omitted wire. A non-optional port can still "
        "show a persistent default; consult the Default / choices column before wiring "
        "a replacement. Choice inputs are selected directly on the component and are "
        "flagged so integer or identifier plumbing is unnecessary.",
        "",
    ]

    components = sorted(
        catalog.components,
        key=lambda component: (
            _product_order(component.product),
            component.category.casefold(),
            component.subcategory.casefold(),
            component.name.casefold(),
            component.runtime_type,
        ),
    )
    current_product: str | None = None
    current_category: str | None = None
    current_subcategory: str | None = None
    for component in components:
        if component.product != current_product:
            current_product = component.product
            current_category = None
            current_subcategory = None
            lines.extend([f"## {component.product}", ""])
        if component.category != current_category:
            current_category = component.category
            current_subcategory = None
            lines.extend([f"### Category: {component.category}", ""])
        if component.subcategory != current_subcategory:
            current_subcategory = component.subcategory
            lines.extend([f"#### Subcategory: {component.subcategory}", ""])
        lines.extend(_component_markdown(component, guides.components[component.runtime_type]))

    lines.extend(
        [
            "## Typed parameter appendix",
            "",
            "Typed parameters are the native Grasshopper containers carried by component "
            "wires. They are listed here for canvas inspection, relays, and data management; "
            "they are not additional modeling steps and do not require users to handle "
            "internal identifiers.",
            "",
        ]
    )
    parameters = sorted(
        catalog.parameters,
        key=lambda parameter: (
            _product_order(parameter.product),
            parameter.category.casefold(),
            parameter.subcategory.casefold(),
            parameter.name.casefold(),
            parameter.runtime_type,
        ),
    )
    current_product = None
    current_category = None
    current_subcategory = None
    grouped_rows: list[tuple[Any, ...]] = []

    def flush_parameter_rows() -> None:
        nonlocal grouped_rows
        if not grouped_rows:
            return
        lines.extend(
            _table(
                ("Parameter", "Nickname", "Wire type", "Exposure", "Description"),
                grouped_rows,
            )
        )
        lines.append("")
        grouped_rows = []

    for parameter in parameters:
        if parameter.product != current_product:
            flush_parameter_rows()
            current_product = parameter.product
            current_category = None
            current_subcategory = None
            lines.extend([f"### {parameter.product}", ""])
        if parameter.category != current_category:
            flush_parameter_rows()
            current_category = parameter.category
            current_subcategory = None
            lines.extend([f"#### Category: {parameter.category}", ""])
        if parameter.subcategory != current_subcategory:
            flush_parameter_rows()
            current_subcategory = parameter.subcategory
            lines.extend([f"##### Subcategory: {parameter.subcategory}", ""])
        grouped_rows.append(
            (
                parameter.name,
                _code(parameter.nickname),
                parameter.friendly_type,
                parameter.exposure,
                parameter.description,
            )
        )
    flush_parameter_rows()

    lines.extend(
        [
            "---",
            "",
            f"Reference completeness: {catalog.component_count} of "
            f"{EXPECTED_COMPONENT_COUNT} public components documented; "
            f"{catalog.parameter_count} standalone typed parameters listed.",
            "",
        ]
    )
    return "\n".join(lines)


def _assert_no_internal_guids(
    text: str,
    internal_guids: frozenset[str],
    location: str,
) -> None:
    lowered = text.casefold()
    leaked = sorted(
        identifier
        for identifier in internal_guids
        if identifier in lowered or identifier.replace("-", "") in lowered
    )
    if leaked:
        # Do not echo the identifiers in this error: logs are also public build artifacts.
        raise UserGuideBuildError(
            f"{location} contains {len(leaked)} internal component or parameter GUID(s)."
        )


def update_text_if_changed(path: Path, text: str) -> bool:
    """Atomically replace a UTF-8 text file only when its bytes changed."""

    payload = text.encode("utf-8")
    if path.is_file() and path.read_bytes() == payload:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.",
        suffix=".tmp",
        dir=path.parent,
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_path, path)
    finally:
        if temporary_path.exists():
            temporary_path.unlink()
    return True


def _load_oodocs() -> SimpleNamespace:
    """Import and verify the exact OODocs API used by this builder."""

    try:
        import oodocs
        from oodocs import (
            Author,
            AuthorLayout,
            Chapter,
            CoverPage,
            Document,
            DocumentMetadata,
            DocumentSettings,
            HeaderFooterDefaults,
            PageBreak,
            PageLayout,
            PageMargins,
            PageNumberDefaults,
            PageSize,
            Section,
            Table,
            TableOfContents,
            Theme,
            TitleMatter,
            TypographyDefaults,
            VerticalSpace,
        )
        from oodocs.importers.markdown import parse_markdown
    except (ImportError, AttributeError) as exc:
        raise UserGuideBuildError(
            "OODocs is unavailable or incomplete. Run 'dev.cmd setup' and use the "
            "repository-local documentation venv."
        ) from exc

    actual_version = getattr(oodocs, "__version__", None)
    if actual_version != EXPECTED_OODOCS_VERSION:
        raise UserGuideBuildError(
            f"Expected OODocs {EXPECTED_OODOCS_VERSION}; imported {actual_version!r}. "
            "Run 'dev.cmd setup' to restore the pinned documentation environment."
        )
    return SimpleNamespace(
        Author=Author,
        AuthorLayout=AuthorLayout,
        Chapter=Chapter,
        CoverPage=CoverPage,
        Document=Document,
        DocumentMetadata=DocumentMetadata,
        DocumentSettings=DocumentSettings,
        HeaderFooterDefaults=HeaderFooterDefaults,
        PageBreak=PageBreak,
        PageLayout=PageLayout,
        PageMargins=PageMargins,
        PageNumberDefaults=PageNumberDefaults,
        PageSize=PageSize,
        Section=Section,
        Table=Table,
        TableOfContents=TableOfContents,
        Theme=Theme,
        TitleMatter=TitleMatter,
        TypographyDefaults=TypographyDefaults,
        VerticalSpace=VerticalSpace,
        parse_markdown=parse_markdown,
    )


def _import_chapter(
    api: SimpleNamespace,
    path: Path,
    fallback_title: str,
    internal_guids: frozenset[str],
    *,
    source_override: str | None = None,
) -> Any:
    if source_override is None and not path.is_file():
        raise UserGuideBuildError(f"Required user-guide source is missing: {path}")
    source = source_override if source_override is not None else path.read_text(encoding="utf-8")
    if not source.strip():
        raise UserGuideBuildError(f"Required user-guide source is blank: {path}")
    _assert_no_internal_guids(source, internal_guids, str(path))
    try:
        imported = api.parse_markdown(
            source,
            numbered=False,
            toc=True,
            heading_level_shift=0,
            base_dir=path.parent,
            import_policy="fail-on-lossy",
            source_name=path.as_posix(),
        )
    except Exception as exc:
        raise UserGuideBuildError(f"OODocs could not import {path}: {exc}") from exc
    if imported.issues:
        # fail-on-lossy should already reject this; keep the invariant explicit.
        raise UserGuideBuildError(
            f"OODocs reported {len(imported.issues)} import issue(s) for {path}."
        )
    if not imported.blocks:
        raise UserGuideBuildError(f"OODocs imported no content from {path}.")

    # OODocs treats a table with automatic placement as a float.  Reference
    # tables must stay beside their Inputs/Outputs labels and may split at row
    # boundaries when a page is full, so make that public placement policy
    # explicit on every imported table (including tables nested in sections).
    def prepare_block(block: Any) -> None:
        if isinstance(block, api.Table):
            block.placement = "here"
            block.split = True
        for child in getattr(block, "children", ()):
            prepare_block(child)
        for item in getattr(block, "items", ()):
            prepare_block(item)

    for imported_block in imported.blocks:
        prepare_block(imported_block)

    blocks = tuple(imported.blocks)
    title = fallback_title
    if len(blocks) == 1 and isinstance(blocks[0], api.Section):
        root = blocks[0]
        if root.level == 1:
            title = root.plain_title().strip() or fallback_title
            blocks = tuple(root.children)
    elif any(
        isinstance(block, api.Section) and block.level == 1
        for block in blocks
    ):
        raise UserGuideBuildError(
            f"{path} must have one top-level Markdown heading with all content beneath it."
        )
    if not blocks:
        raise UserGuideBuildError(f"{path} has a title but no chapter content.")
    return api.Chapter(title, *blocks, numbered=False, toc=True)


def _build_document(
    api: SimpleNamespace,
    repo_root: Path,
    internal_guids: frozenset[str],
    reference_source: str,
) -> Any:
    guide_directory = repo_root / "docs/user-guide"
    chapters = tuple(
        _import_chapter(
            api,
            guide_directory / filename,
            fallback_title,
            internal_guids,
            source_override=reference_source if filename == REFERENCE_PATH.name else None,
        )
        for filename, fallback_title in SOURCE_CHAPTERS
    )
    theme = api.Theme(
        typography=api.TypographyDefaults(
            body_font_name="Malgun Gothic",
            monospace_font_name="Consolas",
            title_font_size=24.0,
            body_font_size=9.5,
            heading_sizes=(18.0, 15.0, 12.5, 11.0, 10.0, 9.5),
            caption_font_size=8.5,
        ),
        page_numbers=api.PageNumberDefaults(
            show_page_numbers=True,
            page_number_alignment="right",
            page_number_template="{page}",
            page_number_font_size=8.0,
        ),
        header_footer=api.HeaderFooterDefaults(
            # OODocs 1.3 resolves running chapter names statically for PDF;
            # a fixed guide label keeps every page accurate across chapters.
            header_left="Dragon Grasshopper User Guide",
            header_right="InvisibleDragon + SimpleDragon",
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
    settings = api.DocumentSettings(
        metadata=api.DocumentMetadata(
            title="InvisibleDragon + SimpleDragon User Guide",
            author="Gonie-Gonie",
            subject="Grasshopper building-energy workflow and component reference",
            keywords=(
                "Grasshopper",
                "Rhino 7",
                "Rhino 8",
                "EnergyPlus",
                "InvisibleDragon",
                "SimpleDragon",
            ),
            description=(
                "User guide for the InvisibleDragon and SimpleDragon Grasshopper plugins."
            ),
        ),
        title_matter=api.TitleMatter(
            subtitle=(
                "Grasshopper-native building energy workflows for Rhino 7 and Rhino 8"
            ),
            authors=(api.Author("Gonie-Gonie"),),
            author_layout=api.AuthorLayout(
                mode="stacked",
                show_affiliations=False,
                show_details=False,
            ),
            cover=api.CoverPage(
                eyebrow="USER GUIDE",
                organization="Gonie-Gonie",
                footer="InvisibleDragon + SimpleDragon",
            ),
        ),
        page_layout=api.PageLayout.portrait(
            api.PageSize.a4(),
            api.PageMargins(top=1.8, right=1.6, bottom=1.8, left=1.6, unit="cm"),
        ),
        theme=theme,
    )
    document_blocks: list[Any] = [
        api.TableOfContents("Contents", max_level=3, show_page_numbers=True)
    ]
    for chapter in chapters:
        # A small ordinary flowable after the explicit break resets ReportLab's
        # frame state when the preceding chapter ends in a long split table.
        # Without it, a following chapter heading can be positioned above the
        # top frame on some page combinations.
        document_blocks.extend(
            (api.PageBreak(), api.VerticalSpace(10, unit="pt"), chapter)
        )
    return api.Document(
        "InvisibleDragon + SimpleDragon User Guide",
        *document_blocks,
        settings=settings,
    )


def render_pdf_only(document: Any, output_path: Path) -> Path:
    """Validate and atomically render one PDF without sidecar formats."""

    if output_path.suffix.lower() != ".pdf":
        raise UserGuideBuildError(
            f"User-guide output must end in .pdf; received {output_path}."
        )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    try:
        document.validate(raise_on_error=True, formats=("pdf",))
    except Exception as exc:
        raise UserGuideBuildError(f"OODocs PDF validation failed: {exc}") from exc

    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{output_path.stem}.",
        suffix=".pdf",
        dir=output_path.parent,
    )
    os.close(descriptor)
    temporary_path = Path(temporary_name)
    try:
        temporary_path.unlink()
        try:
            # ReportLab otherwise writes wall-clock timestamps and a random
            # document ID. Its invariant mode keeps repeated builds stable.
            from reportlab import rl_config

            previous_invariant = rl_config.invariant
            try:
                rl_config.invariant = 1
                rendered = Path(document.save_pdf(temporary_path, validate=False))
            finally:
                rl_config.invariant = previous_invariant
        except Exception as exc:
            raise UserGuideBuildError(f"OODocs PDF rendering failed: {exc}") from exc
        if rendered.resolve() != temporary_path.resolve():
            raise UserGuideBuildError(
                f"OODocs rendered an unexpected output path: {rendered}"
            )
        payload = temporary_path.read_bytes()
        if len(payload) < 1_000 or not payload.startswith(b"%PDF-"):
            raise UserGuideBuildError("OODocs did not create a valid non-empty PDF.")
        os.replace(temporary_path, output_path)
    finally:
        if temporary_path.exists():
            temporary_path.unlink()
    return output_path


def validate_rendered_pdf(
    path: Path,
    catalog: RuntimeCatalog,
    internal_guids: frozenset[str],
) -> None:
    """Postflight the staged PDF before replacing the prior deliverable."""

    try:
        from pypdf import PdfReader

        reader = PdfReader(path)
        if reader.is_encrypted:
            raise UserGuideBuildError("The rendered user-guide PDF is encrypted.")
        if not reader.pages:
            raise UserGuideBuildError("The rendered user-guide PDF has no pages.")
        extracted = "\n".join(page.extract_text() or "" for page in reader.pages)
    except UserGuideBuildError:
        raise
    except Exception as exc:
        raise UserGuideBuildError(
            f"Could not inspect the staged OODocs PDF with pypdf: {exc}"
        ) from exc

    normalized = re.sub(r"\s+", " ", extracted).strip()
    searchable = normalized.casefold()
    chapter_titles = tuple(label for _, label in SOURCE_CHAPTERS)
    missing_chapters = [
        title for title in chapter_titles if title.casefold() not in searchable
    ]
    missing_components = sorted(
        component.name
        for component in catalog.components
        if component.name.casefold() not in searchable
    )
    missing_parameters = sorted(
        parameter.name
        for parameter in catalog.parameters
        if parameter.name.casefold() not in searchable
    )
    if missing_chapters or missing_components or missing_parameters:
        raise UserGuideBuildError(
            "The staged PDF is missing required extracted text; "
            f"chapters={missing_chapters}, components={missing_components}, "
            f"typed_parameters={missing_parameters}."
        )

    coverage = (
        f"Coverage: {catalog.component_count} components and "
        f"{catalog.parameter_count} standalone typed parameters for "
        f"{catalog.framework}."
    )
    if coverage.casefold() not in searchable:
        raise UserGuideBuildError(
            f"The staged PDF is missing its exact runtime coverage statement: {coverage}"
        )

    leaked = sorted(identifier for identifier in internal_guids if identifier in searchable)
    if leaked:
        raise UserGuideBuildError(
            f"The staged PDF leaks {len(leaked)} internal Grasshopper GUID(s)."
        )


def _resolve_from_repo(value: Path, repo_root: Path) -> Path:
    return value.resolve() if value.is_absolute() else (repo_root / value).resolve()


def build_user_guide(
    *,
    repo_root: Path,
    catalog_path: Path,
    output_path: Path,
    compatibility_catalog_paths: Sequence[Path] = (),
) -> BuildSummary:
    """Generate the component reference and build the fixed PDF user guide."""

    repo_root = repo_root.resolve()
    if not repo_root.is_dir():
        raise UserGuideBuildError(f"Repository root does not exist: {repo_root}")
    if not (repo_root / "global.json").is_file():
        raise UserGuideBuildError(
            f"Repository root does not contain global.json: {repo_root}"
        )
    catalog_path = _resolve_from_repo(catalog_path, repo_root)
    compatibility_catalog_paths = tuple(
        _resolve_from_repo(path, repo_root) for path in compatibility_catalog_paths
    )
    output_path = _resolve_from_repo(output_path, repo_root)
    if output_path.suffix.lower() != ".pdf":
        raise UserGuideBuildError(
            f"User-guide output must end in .pdf; received {output_path}."
        )

    catalog, internal_identities = load_runtime_catalog(catalog_path)
    frameworks = [catalog.framework]
    all_internal_guids = set(internal_identities.values())
    for compatibility_path in compatibility_catalog_paths:
        compatibility_catalog, compatibility_identities = load_runtime_catalog(
            compatibility_path
        )
        validate_host_catalog_compatibility(
            catalog,
            internal_identities,
            compatibility_catalog,
            compatibility_identities,
        )
        frameworks.append(compatibility_catalog.framework)
        all_internal_guids.update(compatibility_identities.values())
    if len(frameworks) != len(set(frameworks)):
        raise UserGuideBuildError(
            f"Runtime catalog frameworks must be unique; found {frameworks}."
        )
    catalog = replace(catalog, framework=" + ".join(sorted(frameworks)))
    internal_guids = frozenset(all_internal_guids)
    guides = load_detailed_guides(repo_root / GUIDES_PATH)
    validate_guide_coverage(catalog, guides)
    reference = render_component_reference(catalog, guides)
    _assert_no_internal_guids(reference, internal_guids, str(repo_root / REFERENCE_PATH))

    api = _load_oodocs()
    document = _build_document(api, repo_root, internal_guids, reference)

    # Preserve the prior distributable PDF if metadata validation, Markdown
    # import, or rendering fails. Only publish the staged PDF after the
    # generated reference has also been written successfully.
    output_path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, staged_name = tempfile.mkstemp(
        prefix=f".{output_path.stem}.staged.",
        suffix=".pdf",
        dir=output_path.parent,
    )
    os.close(descriptor)
    staged_pdf = Path(staged_name)
    try:
        staged_pdf.unlink()
        render_pdf_only(document, staged_pdf)
        validate_rendered_pdf(staged_pdf, catalog, internal_guids)
        reference_updated = update_text_if_changed(repo_root / REFERENCE_PATH, reference)
        os.replace(staged_pdf, output_path)
    finally:
        if staged_pdf.exists():
            staged_pdf.unlink()
    rendered = output_path
    return BuildSummary(
        component_count=catalog.component_count,
        parameter_count=catalog.parameter_count,
        reference_updated=reference_updated,
        reference_path=repo_root / REFERENCE_PATH,
        pdf_path=rendered,
    )


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Generate the complete component reference and build the PDF-only "
            "InvisibleDragon + SimpleDragon user guide with OODocs."
        )
    )
    parser.add_argument(
        "--repo-root",
        required=True,
        type=Path,
        help="Repository root containing global.json and docs/user-guide.",
    )
    parser.add_argument(
        "--catalog",
        required=True,
        type=Path,
        help="Runtime component catalog JSON (absolute or repository-relative).",
    )
    parser.add_argument(
        "--output",
        required=True,
        type=Path,
        help="PDF output path (absolute or repository-relative; .pdf only).",
    )
    parser.add_argument(
        "--compatibility-catalog",
        action="append",
        default=[],
        type=Path,
        help=(
            "Additional runtime catalog whose complete public Grasshopper contract "
            "must match --catalog (repeatable)."
        ),
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        summary = build_user_guide(
            repo_root=args.repo_root,
            catalog_path=args.catalog,
            output_path=args.output,
            compatibility_catalog_paths=args.compatibility_catalog,
        )
    except UserGuideBuildError as exc:
        print(f"User guide build failed: {exc}", file=sys.stderr)
        return 1
    print(
        json.dumps(
            {
                "component_count": summary.component_count,
                "parameter_count": summary.parameter_count,
                "reference_updated": summary.reference_updated,
                "reference_path": str(summary.reference_path),
                "pdf_path": str(summary.pdf_path),
            },
            ensure_ascii=False,
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

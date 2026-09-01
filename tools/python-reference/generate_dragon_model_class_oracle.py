"""Generate the pinned ``EnergyModel`` class-surface oracle.

This deliberately narrow corpus targets only inventory index 815, the
``EnergyModel`` class receipt.  It observes the mutable class-level
``supported_versions`` list, mutation visibility/restoration, instance
shadowing, arbitrary instance attributes, and subclassability.  The
constructor and every named member are exact resolved receipts and are never
executed or promoted as targets here.  ``Version`` is context only.
"""

from __future__ import annotations

import argparse
from collections import Counter
import importlib.metadata
import importlib.util
import os
from pathlib import Path
import sys
from typing import Any


SCHEMA = "dragons.python-reference.dragon-model-class.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
MODEL_SOURCE_PATH = "src/idragon/dragon/model.py"
MODEL_SOURCE_SHA256 = (
    "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090"
)
MODEL_AST_SHA256 = (
    "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59"
)
MODEL_SOURCE_BYTES = 8_247

SOURCE_RECEIPTS = (
    ("src/idragon/__init__.py", "sha256:a486e6471fc9afa8f431ee1b63eea9054d8ba757863c617365a515751f881618", "sha256:1d80e812842f6ef6803fedfb9c996a8e50841c4a4399b89230f5178554597e50"),
    ("src/idragon/common.py", "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9", "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d"),
    ("src/idragon/constants.py", "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084", "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520"),
    ("src/idragon/dragon/__init__.py", "sha256:1a1a599171964e2dfda806d66a5c46bb8b8c8514bdf997419a859187d9564d52", "sha256:88df519f22bc3b086d76e318a3a58bb07677da33d2947e1095d0236b270f048a"),
    ("src/idragon/dragon/construction.py", "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a", "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622"),
    ("src/idragon/dragon/hvac.py", "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31", "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0"),
    (MODEL_SOURCE_PATH, MODEL_AST_SHA256, MODEL_SOURCE_SHA256),
    ("src/idragon/dragon/profile.py", "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef", "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445"),
    ("src/idragon/dragon/shape.py", "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2", "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c"),
    ("src/idragon/imugi.py", "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90", "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613"),
    ("src/idragon/launcher.py", "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e", "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f"),
    ("src/idragon/utils.py", "sha256:abda2bfa93ff7461fb412cd1dd8fe526d30983ff22017e714b17dea1aa9f7452", "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd"),
)


def _receipt(
    index: int,
    symbol: str,
    kind: str,
    path: str,
    symbol_hash: str,
    signature_hash: str,
    body_hash: str,
) -> dict[str, Any]:
    return {
        "body_hash": "sha256:" + body_hash,
        "inventory_index": index,
        "kind": kind,
        "path": path,
        "signature_hash": "sha256:" + signature_hash,
        "symbol": symbol,
        "symbol_hash": "sha256:" + symbol_hash,
    }


TARGET_RECEIPTS = (
    _receipt(
        815,
        "EnergyModel",
        "class",
        MODEL_SOURCE_PATH,
        "a7582a410b3e8189778cacda204ee15a6fd3039d6f136f9a9303bb4437fe2170",
        "0e5d2973f067f9c718303cadabe96e5f8ab87d9d83bce8ec4d369a77266db029",
        "5a7db40c87570a3ae22c820c5b758ed19b5e8120a7cbf0510d3a88eb7c7f33d9",
    ),
)

CONTEXT_RECEIPTS = (
    _receipt(556, "Version", "class", "src/idragon/common.py", "1c497416f9054aec72cc23eb32f3740e6001e70183471e0453128ec74d7770c8", "127a8b300808358bf3f1a153c025fb3d53ef73e7fd1ba8cc098576acb458a6ed", "fb7b04e087cf5ee44ca605240380ca8847066ea9c7c879315419dc0b52446c3c"),
    _receipt(558, "Version.__init__", "function", "src/idragon/common.py", "a3def1029c1ebaf97d2c94d1efdc88f0c302c44e0c93d2045c38be0b12a0e983", "03d7516c1730f6f95147d7ebd855ace566e32c4f896eab3ff830b5ba6e716413", "fca44c5193da96a1ce893264f7969f6edb34bc2f579bc0447f87386e417adbce"),
)

RESOLVED_RECEIPTS = (
    _receipt(816, "EnergyModel.__init__", "function", MODEL_SOURCE_PATH, "1d1dbee8fef8b70b2919c4e46a0ea60efbd748b360d31ff353ea121c72ad97d2", "9706dcab3a90048744a47f3596613b34247cb6cd1eb2903582e2fb2cb6342a2d", "e4e5ef56fd12719fe976231c03d867e932eff64870f9c0fd7a5107b7e11538f1"),
    _receipt(817, "EnergyModel.add_supply_system", "function", MODEL_SOURCE_PATH, "174532d0aa6b76826dd78f3d7020ba49eeba26494019da3fb361396e31c15a94", "576bb4584970582d94ae80ad061612e84dad263321a9e6288b39a92af7cd959f", "6bf509a4d5050f54bd748c516ed98b6ae249edf3aaa84a75c4c7bd11b7fbef4b"),
    _receipt(818, "EnergyModel.conditioned_zones", "function", MODEL_SOURCE_PATH, "90ceddf7de437a59950e7081185fefbf1f56354a49662431452f11ac24bc6f24", "e8822bd4d00ab05c8d049de4b8fedb8917e0b9cd2daa2c2a3f7503b1985b276e", "ae71f1c62c76cfdf6890e18c83f3dd2709b9fb72627f690db7dc52b7db719348"),
    _receipt(819, "EnergyModel.create_default_idf", "function", MODEL_SOURCE_PATH, "585b53682bd5dbd4d2081e79eddc2789fa60925baafb5eae26de0541346ac9f4", "6750822d2a0b36e44dced756c45817742cfc0940e8646be6212eedfe3698d8cf", "e505591e57b64f4f7ff0b6fb18e775ad88048d4eaddb9d8a4f9e5a0afd2c8ab7"),
    _receipt(820, "EnergyModel.surfaces", "function", MODEL_SOURCE_PATH, "9bd40b3fbdc974f1f3a7550b2df6ec8f4c41ce9cb55ecbc07b3f2fce264834c0", "175c75a451212fe0099b1206d31f4f11195e5716bdbae2c993097a86e669a0ea", "9ac965df879ac38614b80c38800b8b7e28f3a584d20be71afac9301eea223c06"),
    _receipt(821, "EnergyModel.to_idf", "function", MODEL_SOURCE_PATH, "de10251f38f220956e870d8faea1c7a879da9158b369cffc244f7afc6519eb35", "9389bd00d5a2180ea9f3cd1aa5695ba492e1665947515c34c31eff01f072bade", "9d1b5a610b485aa782c0c1f39ed57b65d5534e1ba3271f1a325c52a109228189"),
    _receipt(822, "EnergyModel.unconditioned_zones", "function", MODEL_SOURCE_PATH, "24b8c9a917df6c286d13dfb75c3ca04403b74cf0a70e6056cc933c9ed2822e08", "e8822bd4d00ab05c8d049de4b8fedb8917e0b9cd2daa2c2a3f7503b1985b276e", "e65c4689f16398a99be21f56cf6c046ee411718b151d637a75abc7e8076249c8"),
    _receipt(823, "EnergyModel.used_constructions", "function", MODEL_SOURCE_PATH, "b34dd26fdb9af00f053278e77ac3cc85394a646405e8e5e0b5c077342fd1bebd", "47d2fe431ebc01347b7bef0a612859f9d45131c67b7ee67971757a0694919023", "56cc7c61d049242fa77c1c2457d6d9f5678ca41a41af86ddd2ff93be20ed78b3"),
    _receipt(824, "EnergyModel.used_layers", "function", MODEL_SOURCE_PATH, "e15c8d38a7b918895bf399bc319bbb2caf2810d416cb4c8792fedb5cec3358f0", "d5bc4e72ec91b9ecdbdd46cd7a50e3da18408ff227d9549c7ae42bf488381844", "bde4ae4c3efe1129e1c3ee19dc273a7e251f770f7173fbc1a3d2b67ec80d0733"),
    _receipt(825, "EnergyModel.used_profiles", "function", MODEL_SOURCE_PATH, "b8a8a5f692a0cbeeec4215cbab71e89291a3f96e68d7702853631dc454a695ab", "2417ee894af42b33af27bb335ee1a91c7205d1a2093879c28e6e4178554e4a60", "5e04e97f3e1161b94743a1377272a037c646df7e0aa07b6a3ce51c3d4b61ae9a"),
)

TARGET_SYMBOLS = ("EnergyModel",)
CONTEXT_SYMBOLS = tuple(item["symbol"] for item in CONTEXT_RECEIPTS)
RESOLVED_SYMBOLS = tuple(item["symbol"] for item in RESOLVED_RECEIPTS)
ALL_RECEIPTS = TARGET_RECEIPTS + CONTEXT_RECEIPTS + RESOLVED_RECEIPTS

ADAPTATION = "sealed-read-only-native-energy-model-class-a7582a41"
ASSERTION_ID = "dragon-model-energy-model-class-a7582a41"
EXPECTED_CASE_IDS = (
    "dragon-model-energy-model-class.c01-class-supported-versions-topology",
    "dragon-model-energy-model-class.c02-shared-list-append-visibility-restoration",
    "dragon-model-energy-model-class.c03-instance-shadow-arbitrary-attribute-subclass-topology",
)
EXPECTED_CASE_COUNT = 3
EXPECTED_FACT_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:62b1cb7d44213516784a823cce69a6204e8e265107202c0ab06cc0b0197827a8",
    EXPECTED_CASE_IDS[1]: "sha256:69e12bb56ad9212f0380b92b1c7327f8f9875bd1a8e2e3aa42f6cc88fed04aa4",
    EXPECTED_CASE_IDS[2]: "sha256:e9043277693366f6b7c69a36c46da49b7dcbfcda30a180a0888567192d76bceb",
}
EXPECTED_CASE_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:77e860eaed52a820286e037d3966e17135f7866dd306c3bcfc478fd44d47cb4a",
    EXPECTED_CASE_IDS[1]: "sha256:6268a6f473231cc8f3d88043a51ef7721c16f1372ae2edbae0d122b7bfea2a29",
    EXPECTED_CASE_IDS[2]: "sha256:9a7170a7a3678ce436b450e2527d348fbca97cf817793b2fc00e8608ec6a0810",
}
EXPECTED_CASES_SHA256 = (
    "sha256:ab27c0de1d256d0942a8db49523fe3ba3d6701ddd469684c2261818518f95a59"
)

REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_dragons_dragon_model_class_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load EnergyModel class oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or tuple(module.SOURCE_RECEIPTS) != SOURCE_RECEIPTS
    ):
        raise RuntimeError("EnergyModel class oracle support is not exactly pinned.")
    return module


CORE = _load_core_support()
SUPPORT = CORE.SUPPORT
HELPER = SUPPORT.SUPPORT
EXPECTED_DEPENDENCIES = CORE.EXPECTED_DEPENDENCIES
strict_json_dumps = CORE.strict_json_dumps
canonical_sha256 = CORE.canonical_sha256
sha256_file = CORE.sha256_file
load_json_without_duplicates = CORE.load_json_without_duplicates
RAW_ADDRESS_PATTERN = CORE.RAW_ADDRESS_PATTERN
ABSOLUTE_PATH_PATTERN = CORE.ABSOLUTE_PATH_PATTERN
GUID_PATTERN = CORE.GUID_PATTERN
TIMESTAMP_PATTERN = CORE.TIMESTAMP_PATTERN


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def _indexed(receipts: tuple[dict[str, Any], ...]) -> list[dict[str, Any]]:
    return [dict(receipt) for receipt in receipts]


def _symbols_for_path(path: str) -> tuple[str, ...]:
    return tuple(
        receipt["symbol"]
        for receipt in sorted(
            (item for item in ALL_RECEIPTS if item["path"] == path),
            key=lambda item: item["inventory_index"],
        )
    )


SOURCE_SPECS = tuple(
    {
        "ast_sha256": ast_hash,
        "path": path,
        "source_sha256": source_hash,
        "symbols": _symbols_for_path(path),
    }
    for path, ast_hash, source_hash in SOURCE_RECEIPTS
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _load_source_inventory(
    path: Path, commit: str, source: dict[str, Any]
) -> dict[str, Any]:
    expected = {receipt["symbol"]: _descriptor(receipt) for receipt in ALL_RECEIPTS}
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(HELPER, name) for name in names}
    try:
        HELPER.SOURCE_PATH = source["path"]
        HELPER.EXPECTED_SOURCE_SHA256 = source["source_sha256"]
        HELPER.EXPECTED_SYMBOL_HASHES = {
            symbol: expected[symbol]["symbol_hash"] for symbol in source["symbols"]
        }
        HELPER.TARGET_SYMBOLS = tuple(source["symbols"])
        result = HELPER.load_exact_inventory(path, commit)
    finally:
        for name, value in original.items():
            setattr(HELPER, name, value)

    expected_file = {
        "ast_hash": source["ast_sha256"],
        "content_hash": source["source_sha256"],
        "path": source["path"],
    }
    expected_symbols = [expected[symbol] for symbol in source["symbols"]]
    if result["file"] != expected_file or result["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return result


def load_exact_inventory(path: Path, commit: str) -> dict[str, Any]:
    raw = load_json_without_duplicates(path)
    inventories = [
        _load_source_inventory(path, commit, source) for source in SOURCE_SPECS
    ]
    if any(
        item["content_sha256"] != EXPECTED_INVENTORY_SHA256
        for item in inventories
    ):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    for receipt in ALL_RECEIPTS:
        observed = {
            **raw["symbols"][receipt["inventory_index"]],
            "inventory_index": receipt["inventory_index"],
        }
        if observed != receipt:
            raise SystemExit(
                f"Exact indexed EnergyModel receipt drifted: {receipt['symbol']}."
            )
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "context_receipts": _indexed(CONTEXT_RECEIPTS),
        "files": [item["file"] for item in inventories],
        "resolved_receipts": _indexed(RESOLVED_RECEIPTS),
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": _indexed(TARGET_RECEIPTS),
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    subfamilies = ("class-topology", "shared-class-state", "open-instance-type")
    return tuple(
        {
            "context_symbols": list(CONTEXT_SYMBOLS),
            "executor": "energy-model-class",
            "expected_dotnet": {
                "adaptations": [ADAPTATION],
                "classifications": {"EnergyModel": "exception"},
                "outcome": "adapted-as-pinned",
            },
            "id": identifier,
            "subfamily": subfamily,
            "target_symbols": ["EnergyModel"],
        }
        for identifier, subfamily in zip(
            EXPECTED_CASE_IDS, subfamilies, strict=True
        )
    )


def _tag(value: Any) -> dict[str, Any]:
    if value is None:
        return {"kind": "none"}
    if type(value) is bool:
        return {"kind": "bool", "value": value}
    if type(value) is int:
        return {"kind": "int", "value": str(value)}
    if type(value) is str:
        return {"kind": "str", "value": value}
    raise RuntimeError(f"Unsupported EnergyModel class scalar: {type(value).__name__}")


def _version_facts(value: Any, modules: Any) -> dict[str, Any]:
    attributes = vars(value)
    expected_names = (
        "_Version__major",
        "_Version__minor",
        "_Version__patch",
    )
    if tuple(attributes) != expected_names:
        raise RuntimeError("Pinned Version private storage topology drifted.")
    return {
        "attribute_names": list(attributes),
        "components": [_tag(attributes[name]) for name in expected_names],
        "is_imported_version_instance": type(value) is modules.common.Version,
        "module": type(value).__module__,
        "type": type(value).__name__,
    }


def _public_descriptor_topology(energy_model: type[Any]) -> list[dict[str, str]]:
    return [
        {"name": name, "storage_type": type(value).__name__}
        for name, value in vars(energy_model).items()
        if not name.startswith("_")
    ]


def _execute_case(identifier: str, modules: Any) -> dict[str, Any]:
    energy_model = modules.model.EnergyModel
    shared = energy_model.supported_versions

    if identifier == EXPECTED_CASE_IDS[0]:
        return {
            "class_topology": {
                "direct_base_names": [base.__name__ for base in energy_model.__bases__],
                "metaclass_name": type(energy_model).__name__,
                "module": energy_model.__module__,
                "name": energy_model.__name__,
                "qualname": energy_model.__qualname__,
            },
            "declared_public_member_topology": _public_descriptor_topology(
                energy_model
            ),
            "supported_versions": {
                "class_dictionary_contains_name": "supported_versions"
                in vars(energy_model),
                "class_dictionary_value_is_read_value": vars(energy_model)[
                    "supported_versions"
                ]
                is shared,
                "container_type": type(shared).__name__,
                "count": _tag(len(shared)),
                "items": [_version_facts(value, modules) for value in shared],
            },
        }

    if identifier == EXPECTED_CASE_IDS[1]:
        before = list(shared)
        original_container = shared
        blank_instance = object.__new__(energy_model)

        class EnergyModelAppendVisibilityProbe(energy_model):
            pass

        mutation: dict[str, Any]
        try:
            appended = modules.common.Version(25, 1, 0)
            shared.append(appended)
            mutation = {
                "appended_item": _version_facts(appended, modules),
                "appended_item_identity_preserved": shared[-1] is appended,
                "class_count": _tag(len(energy_model.supported_versions)),
                "class_read_is_original_container": energy_model.supported_versions
                is original_container,
                "instance_count": _tag(len(blank_instance.supported_versions)),
                "instance_read_is_original_container": blank_instance.supported_versions
                is original_container,
                "subclass_count": _tag(
                    len(EnergyModelAppendVisibilityProbe.supported_versions)
                ),
                "subclass_read_is_original_container": (
                    EnergyModelAppendVisibilityProbe.supported_versions
                    is original_container
                ),
                "visible_items": [
                    _version_facts(value, modules) for value in shared
                ],
            }
        finally:
            shared[:] = before

        return {
            "before": {
                "count": _tag(len(before)),
                "items": [_version_facts(value, modules) for value in before],
            },
            "mutation": mutation,
            "restoration": {
                "class_read_is_original_container": energy_model.supported_versions
                is original_container,
                "contents_equal_by_identity": len(shared) == len(before)
                and all(left is right for left, right in zip(shared, before)),
                "count": _tag(len(shared)),
                "items": [_version_facts(value, modules) for value in shared],
            },
        }

    if identifier == EXPECTED_CASE_IDS[2]:
        instance = object.__new__(energy_model)
        initial_names = sorted(vars(instance))
        shadow = ["instance-only-version-shadow"]
        marker = ("arbitrary", "attribute")
        instance.supported_versions = shadow
        instance.review_marker = marker

        class EnergyModelSubclassTopologyProbe(energy_model):
            pass

        subclass_instance = object.__new__(EnergyModelSubclassTopologyProbe)
        subclass_instance.subclass_marker = "subclass-instance-attribute"
        return {
            "instance_topology": {
                "arbitrary_attribute_names": sorted(vars(instance)),
                "arbitrary_attribute_type": type(instance.review_marker).__name__,
                "arbitrary_attribute_value": [
                    _tag(value) for value in instance.review_marker
                ],
                "class_supported_versions_unchanged": energy_model.supported_versions
                is shared,
                "created_without_constructor": initial_names == [],
                "initial_attribute_names": initial_names,
                "instance_is_energy_model": isinstance(instance, energy_model),
                "shadow_container_type": type(instance.supported_versions).__name__,
                "shadow_is_class_container": instance.supported_versions is shared,
                "shadow_is_input_container": instance.supported_versions is shadow,
                "shadow_value": [_tag(value) for value in instance.supported_versions],
            },
            "subclass_topology": {
                "arbitrary_instance_attribute_roundtrip": _tag(
                    subclass_instance.subclass_marker
                ),
                "base_instance_check": isinstance(subclass_instance, energy_model),
                "direct_base_names": [
                    base.__name__
                    for base in EnergyModelSubclassTopologyProbe.__bases__
                ],
                "inherited_supported_versions_is_class_container": (
                    EnergyModelSubclassTopologyProbe.supported_versions is shared
                ),
                "mro_names": [
                    item.__name__ for item in EnergyModelSubclassTopologyProbe.__mro__
                ],
                "name": EnergyModelSubclassTopologyProbe.__name__,
                "subclass_definition_outcome": "returned",
            },
        }

    raise RuntimeError(f"Unknown EnergyModel class case: {identifier}")


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def case_sha256(cases: list[dict[str, Any]]) -> dict[str, str]:
    return {case["id"]: canonical_sha256(case) for case in cases}


def _module_name(source_path: str) -> str:
    relative = Path(source_path).relative_to("src").with_suffix("")
    parts = list(relative.parts)
    if parts[-1] == "__init__":
        parts.pop()
    return ".".join(parts)


def _expected_loaded_local_modules() -> list[dict[str, str]]:
    return [
        {
            "ast_sha256": source["ast_sha256"],
            "module": _module_name(source["path"]),
            "path": source["path"],
            "source_sha256": source["source_sha256"],
        }
        for source in SOURCE_SPECS
    ]


def _expected_files() -> list[dict[str, str]]:
    return [
        {
            "ast_hash": source["ast_sha256"],
            "content_hash": source["source_sha256"],
            "path": source["path"],
        }
        for source in SOURCE_SPECS
    ]


def _expected_inventory() -> dict[str, Any]:
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "context_receipts": _indexed(CONTEXT_RECEIPTS),
        "files": _expected_files(),
        "resolved_receipts": _indexed(RESOLVED_RECEIPTS),
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": _indexed(TARGET_RECEIPTS),
    }


def _expected_upstream() -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "loaded_local_modules": _expected_loaded_local_modules(),
        "model_source": {
            "ast_sha256": MODEL_AST_SHA256,
            "bytes": MODEL_SOURCE_BYTES,
            "path": MODEL_SOURCE_PATH,
            "source_sha256": MODEL_SOURCE_SHA256,
        },
        "sources": [
            {
                "ast_sha256": source["ast_sha256"],
                "path": source["path"],
                "source_sha256": source["source_sha256"],
            }
            for source in SOURCE_SPECS
        ],
    }


def _expected_consumer_contract() -> dict[str, Any]:
    return {
        "adaptations": {"EnergyModel": ADAPTATION},
        "assertion_ids": {"EnergyModel": ASSERTION_ID},
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_basis": (
            "Pinned Python EnergyModel is subclassable, exposes an inherited mutable "
            "class-level supported_versions list, and permits instance shadowing and "
            "arbitrary attributes. The native EnergyModel is sealed and exposes a fresh "
            "read-only SupportedVersions collection while retaining its validated, typed "
            "model contract."
        ),
        "classification_counts": {"equivalent": 0, "exception": 1},
        "classifications": {"EnergyModel": "exception"},
        "closure": {
            "case_coverage_by_symbol": {
                "EnergyModel": list(EXPECTED_CASE_IDS),
            },
            "context_receipts": _indexed(CONTEXT_RECEIPTS),
            "full_symbol_closure": False,
            "resolved_receipts_not_retargeted": _indexed(RESOLVED_RECEIPTS),
            "scope": "exact-three-case-energy-model-class-surface",
            "target_coverage_complete": True,
            "target_symbols": ["EnergyModel"],
            "unresolved_boundaries": [
                "concurrent-supported_versions-mutation",
                "metaclass-or-descriptor-monkey-patching",
                "custom-subclass-hooks-and-multiple-inheritance",
                "Version-behavior-beyond-the-two-observed-context-instances",
            ],
        },
        "identity_encoding": "stable-direct-is-relations-only-no-id-or-address",
        "native_targets": {
            "EnergyModel": "Dragons.InvisibleDragon.Model.EnergyModel"
        },
        "raw_fact_encoding": "typed-scalars-plus-stable-topology-and-direct-identity-relations",
        "source_import_policy": (
            "external-temporary-copy-with-complete-loaded-local-module-audit"
        ),
        "target_receipts": _indexed(TARGET_RECEIPTS),
        "target_symbols": ["EnergyModel"],
    }


def _dependencies() -> dict[str, str]:
    result: dict[str, str] = {}
    for distribution in EXPECTED_DEPENDENCIES:
        try:
            result[distribution] = importlib.metadata.version(distribution)
        except importlib.metadata.PackageNotFoundError as error:
            raise RuntimeError(
                f"Required reference dependency is missing: {distribution}"
            ) from error
    return result


def _expected_runtime() -> dict[str, Any]:
    return {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "python_dont_write_bytecode": True,
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(source["path"]).relative_to("src")


def _find_pinned_source_root() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        root = Path(entry)
        if all(
            _source_file(root, source).is_file()
            and sha256_file(_source_file(root, source)) == source["source_sha256"]
            for source in SOURCE_SPECS
        ):
            matches.append(root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


def build_oracle(
    inventory: dict[str, Any], commit: str, source_root: Path | None = None
) -> dict[str, Any]:
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    if inventory != _expected_inventory():
        raise SystemExit("The aggregate EnergyModel class inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")
    model_file = imported_root / Path(MODEL_SOURCE_PATH).relative_to("src")
    if model_file.stat().st_size != MODEL_SOURCE_BYTES:
        raise SystemExit("Pinned dragon/model.py byte length drifted.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        observed = {
            definition["id"]: _execute_case(definition["id"], modules)
            for definition in case_definitions()
        }
        fact_hashes = {
            identifier: canonical_sha256(facts)
            for identifier, facts in observed.items()
        }
        if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
            raise SystemExit(
                "Pinned EnergyModel class facts drifted.\nOBSERVED_FACT_HASHES\n"
                + strict_json_dumps(fact_hashes, indent=2)
            )
        cases: list[dict[str, Any]] = []
        for definition in case_definitions():
            identifier = definition["id"]
            case = dict(definition)
            case["python"] = {
                "facts": observed[identifier],
                "facts_sha256": fact_hashes[identifier],
                "outcome": "observed",
            }
            cases.append(case)
        case_hashes = case_sha256(cases)
        if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
            raise SystemExit(
                "Pinned EnergyModel class case records drifted.\nOBSERVED_CASE_HASHES\n"
                + strict_json_dumps(case_hashes, indent=2)
            )
        aggregate_case_hash = cases_sha256(cases)
        if aggregate_case_hash != EXPECTED_CASES_SHA256:
            raise SystemExit(
                "Pinned EnergyModel class aggregate cases hash drifted: "
                + aggregate_case_hash
            )
        result = {
            "case_sha256": case_hashes,
            "cases": cases,
            "cases_sha256": aggregate_case_hash,
            "consumer_contract": _expected_consumer_contract(),
            "context_receipts": inventory["context_receipts"],
            "fact_sha256": fact_hashes,
            "resolved_receipts": inventory["resolved_receipts"],
            "runtime": {
                "dependencies": _dependencies(),
                "implementation": sys.implementation.name,
                "python_dont_write_bytecode": sys.dont_write_bytecode,
                "python_hash_algorithm": sys.hash_info.algorithm,
                "python_hash_seed": 0,
                "python_hash_width_bits": sys.hash_info.width,
                "python_version": ".".join(map(str, sys.version_info[:3])),
            },
            "schema": SCHEMA,
            "symbols": inventory["symbols"],
            "target_receipts": inventory["target_receipts"],
            "upstream": {
                **_expected_upstream(),
                "commit": commit,
                "loaded_local_modules": modules.loaded_local_modules,
                "sources": [
                    {
                        "ast_sha256": source["ast_sha256"],
                        "path": source["path"],
                        "source_sha256": sha256_file(
                            _source_file(imported_root, source)
                        ),
                    }
                    for source in SOURCE_SPECS
                ],
            },
        }
    validate_oracle(result)
    return result


def _require_keys(value: Any, expected: set[str], location: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(f"{location} key set drifted: {actual!r}")


def _validate_tag(value: dict[str, Any], location: str) -> bool:
    kind = value.get("kind")
    if kind == "none":
        _require_keys(value, {"kind"}, location)
        return True
    if kind == "bool":
        _require_keys(value, {"kind", "value"}, location)
        if type(value["value"]) is not bool:
            raise RuntimeError(f"Invalid tagged bool at {location}.")
        return True
    if kind == "int":
        _require_keys(value, {"kind", "value"}, location)
        try:
            if str(int(value["value"])) != value["value"]:
                raise ValueError
        except (TypeError, ValueError) as error:
            raise RuntimeError(f"Invalid tagged int at {location}.") from error
        return True
    if kind == "str":
        _require_keys(value, {"kind", "value"}, location)
        if type(value["value"]) is not str:
            raise RuntimeError(f"Invalid tagged string at {location}.")
        return True
    return False


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if type(value) is float:
        raise RuntimeError(f"Raw float is forbidden at {location}.")
    if isinstance(value, Path):
        raise RuntimeError(f"Raw path is forbidden at {location}.")
    if isinstance(value, str):
        if ABSOLUTE_PATH_PATTERN.search(value):
            raise RuntimeError(f"Absolute path is forbidden at {location}.")
        if RAW_ADDRESS_PATTERN.search(value):
            raise RuntimeError(f"Raw address is forbidden at {location}.")
        if GUID_PATTERN.search(value):
            raise RuntimeError(f"GUID-like value is forbidden at {location}.")
        if TIMESTAMP_PATTERN.search(value):
            raise RuntimeError(f"Timestamp is forbidden at {location}.")
        return
    if value is None or type(value) in (bool, int):
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_safe_tree(item, f"{location}[{index}]")
        return
    if isinstance(value, dict):
        if "kind" in value and _validate_tag(value, location):
            return
        for key, item in value.items():
            if type(key) is not str:
                raise RuntimeError(f"Non-string JSON key at {location}.")
            _validate_safe_tree(key, f"{location}.<key>")
            _validate_safe_tree(item, f"{location}.{key}")
        return
    raise RuntimeError(f"Unsupported JSON value at {location}: {type(value).__name__}")


def _validate_case_semantics(identifier: str, facts: dict[str, Any]) -> None:
    expected_hash = EXPECTED_FACT_SHA256.get(identifier)
    if expected_hash and canonical_sha256(facts) != expected_hash:
        raise RuntimeError(f"EnergyModel class canonical semantics drifted: {identifier}")

    if identifier == EXPECTED_CASE_IDS[0]:
        valid = (
            facts["class_topology"]
            == {
                "direct_base_names": ["object"],
                "metaclass_name": "type",
                "module": "idragon.dragon.model",
                "name": "EnergyModel",
                "qualname": "EnergyModel",
            }
            and facts["supported_versions"]["container_type"] == "list"
            and facts["supported_versions"]["count"] == _tag(1)
            and facts["supported_versions"]["items"][0]["components"]
            == [_tag(24), _tag(2), _tag(0)]
            and [item["name"] for item in facts["declared_public_member_topology"]]
            == [
                "supported_versions",
                "surfaces",
                "used_constructions",
                "used_layers",
                "used_profiles",
                "conditioned_zones",
                "unconditioned_zones",
                "create_default_idf",
                "add_supply_system",
                "to_idf",
            ]
        )
    elif identifier == EXPECTED_CASE_IDS[1]:
        valid = (
            facts["before"]["count"] == _tag(1)
            and facts["mutation"]["class_count"] == _tag(2)
            and facts["mutation"]["instance_count"] == _tag(2)
            and facts["mutation"]["subclass_count"] == _tag(2)
            and facts["mutation"]["appended_item"]["components"]
            == [_tag(25), _tag(1), _tag(0)]
            and all(
                facts["mutation"][name]
                for name in (
                    "appended_item_identity_preserved",
                    "class_read_is_original_container",
                    "instance_read_is_original_container",
                    "subclass_read_is_original_container",
                )
            )
            and facts["restoration"]["class_read_is_original_container"]
            and facts["restoration"]["contents_equal_by_identity"]
            and facts["restoration"]["count"] == _tag(1)
        )
    elif identifier == EXPECTED_CASE_IDS[2]:
        instance = facts["instance_topology"]
        subclass = facts["subclass_topology"]
        valid = (
            instance["created_without_constructor"]
            and instance["instance_is_energy_model"]
            and instance["class_supported_versions_unchanged"]
            and not instance["shadow_is_class_container"]
            and instance["shadow_is_input_container"]
            and subclass["base_instance_check"]
            and subclass["inherited_supported_versions_is_class_container"]
            and subclass["direct_base_names"] == ["EnergyModel"]
            and subclass["mro_names"]
            == ["EnergyModelSubclassTopologyProbe", "EnergyModel", "object"]
            and subclass["subclass_definition_outcome"] == "returned"
        )
    else:
        valid = False
    if not valid:
        raise RuntimeError(f"EnergyModel class semantic invariant drifted: {identifier}")


def validate_oracle(value: dict[str, Any]) -> None:
    _require_keys(
        value,
        {
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
            "context_receipts",
            "fact_sha256",
            "resolved_receipts",
            "runtime",
            "schema",
            "symbols",
            "target_receipts",
            "upstream",
        },
        "root",
    )
    if value["schema"] != SCHEMA:
        raise RuntimeError("EnergyModel class schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("EnergyModel class cases hash drifted.")
    if value["case_sha256"] != case_sha256(value["cases"]):
        raise RuntimeError("EnergyModel class per-case hash map drifted.")
    _validate_safe_tree(value)

    definitions = case_definitions()
    cases = value["cases"]
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
        or list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS)
        or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT
    ):
        raise RuntimeError("EnergyModel class case order/count drifted.")
    definitions_by_id = {item["id"]: item for item in definitions}
    fact_hashes: dict[str, str] = {}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"EnergyModel class case contract drifted: {case['id']}")
        _require_keys(case["python"], {"facts", "facts_sha256", "outcome"}, "python")
        if case["python"]["outcome"] != "observed":
            raise RuntimeError(f"EnergyModel class Python outcome drifted: {case['id']}")
        fact_hash = canonical_sha256(case["python"]["facts"])
        if case["python"]["facts_sha256"] != fact_hash:
            raise RuntimeError(f"EnergyModel class inline fact hash drifted: {case['id']}")
        fact_hashes[case["id"]] = fact_hash
        _validate_case_semantics(case["id"], case["python"]["facts"])
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("EnergyModel class fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and value["fact_sha256"] != EXPECTED_FACT_SHA256:
        raise RuntimeError("EnergyModel class expected fact hashes drifted.")
    if EXPECTED_CASE_SHA256 and value["case_sha256"] != EXPECTED_CASE_SHA256:
        raise RuntimeError("EnergyModel class expected case hashes drifted.")
    if value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("EnergyModel class expected aggregate cases hash drifted.")

    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({"EnergyModel": 3}):
        raise RuntimeError("EnergyModel class target coverage drifted.")
    if set(RESOLVED_SYMBOLS).intersection(counts):
        raise RuntimeError("Resolved EnergyModel members were retargeted.")
    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("EnergyModel class consumer contract drifted.")
    if value["context_receipts"] != _indexed(CONTEXT_RECEIPTS):
        raise RuntimeError("EnergyModel class context receipts drifted.")
    if value["resolved_receipts"] != _indexed(RESOLVED_RECEIPTS):
        raise RuntimeError("EnergyModel class resolved receipts drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("EnergyModel class runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("EnergyModel class upstream receipts drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("EnergyModel class symbol descriptor drifted.")
    if value["target_receipts"] != _indexed(TARGET_RECEIPTS):
        raise RuntimeError("EnergyModel class target receipt drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for this oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if not sys.dont_write_bytecode:
        raise SystemExit("Bytecode writes must be disabled for the pinned checkout.")
    if (
        sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM
        or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS
    ):
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    if _dependencies() != EXPECTED_DEPENDENCIES:
        raise SystemExit("The exact pinned Python dependency set is required.")
    commit = args.upstream_commit.lower()
    inventory = load_exact_inventory(args.inventory, commit)
    result = build_oracle(inventory, commit)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        strict_json_dumps(result, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"Wrote dragon model EnergyModel class oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

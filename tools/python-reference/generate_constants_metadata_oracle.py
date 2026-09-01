"""Generate the pinned ``idragon/constants.py`` metadata/path oracle.

The bounded corpus covers only the eight unresolved public declarations in
``Directory`` and ``PackageInfo``.  It imports byte-identical copies of the
pinned module at two isolated locations so that path derivation is executed
without serializing host paths.  The already-resolved ``SpecialTag`` family is
receipt-bound as an exclusion and is never executed or promoted as a target.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
import importlib.metadata
import importlib.util
import inspect
import os
from pathlib import Path
import re
import shutil
import sys
import tempfile
from types import ModuleType
from typing import Any, Callable, Iterator


SCHEMA = "dragons.python-reference.constants-metadata.v1"
SOURCE_PATH = "src/idragon/constants.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_INVENTORY_FILE_BYTES = 518_067
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8"
)
EXPECTED_SOURCE_BYTES = 2_590
EXPECTED_SOURCE_SHA256 = (
    "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084"
)


def _receipt(
    index: int,
    symbol: str,
    kind: str,
    symbol_hash: str,
    signature_hash: str,
    body_hash: str,
) -> dict[str, Any]:
    return {
        "body_hash": "sha256:" + body_hash,
        "inventory_index": index,
        "kind": kind,
        "path": SOURCE_PATH,
        "signature_hash": "sha256:" + signature_hash,
        "symbol": symbol,
        "symbol_hash": "sha256:" + symbol_hash,
    }


TARGET_RECEIPTS = (
    _receipt(568, "Directory", "class", "5b876ad7fd9b11f66cc01ecb6c43d4e143b6f0258ba070c02551d968dd68aaf6", "9b095b8323bc225f2dc984ce84b448beb1c9ca385a260e7fc7fa0e20e9518d24", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    _receipt(569, "Directory.ENERGYPLUS_DIR", "constant", "7e01ceac3f311fa9fbf2fde2b25cc1c7cd16c3b3f16a3dae9f55531d25ecef5d", "3b7cde5117ef1f4f50cc31536156cfc47972e4891e14cef1873dbb21670bec45", "4c60beb875b71c3866ad7b2f6c4c2976c58edba859a3eb364608665539a37a30"),
    _receipt(570, "Directory.IDD_DIR", "constant", "1f0c2815e4e0732316c71edc653a9a35e5081466805dfbf900c10971f1d171d5", "fc2b368da7a4f29b674e0243a9cc5f51932a415e16ce648aaa6f0952f2d5b803", "611dabcf2c487823916965244bd620d3e2e8142f13418e7037648c2412df96b4"),
    _receipt(571, "Directory.PROFILE_DIR", "constant", "f65d5eaefa2bc1cbb6f0c9b5904624194a1551f48e7966c7973d35526bad4fa6", "e63d078f01a7657c55c23cdbdaa3fdc0b1bf9367a911885dd5706e22cf728d36", "14d09e816f44d227fd4799d8ebd5d1c6d1f0fccb28985882f54864bb86696fe8"),
    _receipt(572, "PackageInfo", "class", "aaf5b98d4a7dc29f83b698f1fb2881b7bb258885bd2aaf17a53b6da902d1eda1", "2740bb2f2c36f7a928b58073cf72c4f955c0b9fbbb13d6586049071934b22209", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    _receipt(573, "PackageInfo.NAME", "constant", "3942a963fcf59af7b1a181bea940b7a883dec4f7059b042451842334e47768cd", "8a07b85ef52202817199529eb85bd9e57dc995f6e07f09ced1aeec0baf40513e", "cc58b284eb83d3af52e586f2af522b33b3dcb4a63d5675f752047b256874cce3"),
    _receipt(574, "PackageInfo.REQUIRED_PYTHON", "constant", "cf74d0eb707a3668aa515bdd31d767109337841bcf28f03b96c6e9264d9407a4", "bda307293305fe13f76bb51ed2cdbf08110bf353393c5a3ba9b2c6e48c1825a8", "1f50b949f3e09514616d8d527374472d470a2693413a93ccf8df89205c4814c2"),
    _receipt(575, "PackageInfo.VERSION", "constant", "a8260e5f38f8422e1ac38ce24fd0136b4bb3a4de24f268e9a262aa6034031ea4", "5c9774e81f3886d7a93f4152480e1ed58f8749a486ce411bd4e0830807b1e6e7", "81e16ccea394a6f22e27d6a26210439f9099e8217d9d38c3f411ae7bd3f43936"),
)

RESOLVED_RECEIPTS = (
    _receipt(576, "SpecialTag", "class", "3a4b37818bef17a26ede76602478983f0d70840c5a61fce8475f47e491466e41", "2d310be6e0c12953280b4ae7c32d74687bf07cf40743879660dcbd25a74b4cc3", "0f180b3be66f76d002ae59f4b778f5dda999b86b84a380a298e4c5ee331e1fa9"),
    _receipt(577, "SpecialTag.__format__", "function", "4ef932bb8135c4cfaf7e17e805cfb299e50d9400f4a106605bdb2fb75477d3a0", "9cdfbe97dbd56c9709c1449cead8a30f8c529922f871002291db5ef625709ba0", "6446560bba87c8aff916dace718057f6b9a03bb1ea1d04171ece5cb8516bc6c8"),
    _receipt(578, "SpecialTag.__repr__", "function", "f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8"),
    _receipt(579, "SpecialTag.__str__", "function", "13ed292afebbf1a59717e776df9d6ba3e220d2cc248ac2cc450deab9c2261c98", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "41f39e586a619e17144cefe663b44ef26f20f8e6dcb13433bcb31ebd4c066f1f"),
)

TARGET_SYMBOLS = tuple(item["symbol"] for item in TARGET_RECEIPTS)
RESOLVED_SYMBOLS = tuple(item["symbol"] for item in RESOLVED_RECEIPTS)
ALL_RECEIPTS = TARGET_RECEIPTS + RESOLVED_RECEIPTS

CLASSIFICATIONS = {symbol: "exception" for symbol in TARGET_SYMBOLS}
ADAPTATIONS = {
    "Directory": "resolved-native-runtime-and-resource-layout",
    "Directory.ENERGYPLUS_DIR": "explicit-validated-native-energyplus-runtime-root",
    "Directory.IDD_DIR": "validated-native-idd-path-resolution",
    "Directory.PROFILE_DIR": "typed-native-profile-data-without-package-profile-directory",
    "PackageInfo": "static-native-package-information",
    "PackageInfo.NAME": "native-invisibledragon-package-name",
    "PackageInfo.REQUIRED_PYTHON": "compiled-native-target-framework-contract",
    "PackageInfo.VERSION": "native-semantic-version-string",
}
ASSERTION_IDS = {
    item["symbol"]: (
        f"constants-metadata-{item['inventory_index']}-{item['symbol_hash'][7:15]}"
    )
    for item in TARGET_RECEIPTS
}
NATIVE_ADAPTATION_CANDIDATES = {
    "Directory": "Dragons.EnergyPlus.Runtime.RuntimeResolver and caller-supplied resource paths",
    "Directory.ENERGYPLUS_DIR": "EnergyPlusRuntimeLayout.RootPath after manifest and payload validation",
    "Directory.IDD_DIR": "EnergyPlusRuntimeLayout.IddPath or an explicit Grasshopper IDD path",
    "Directory.PROFILE_DIR": "typed Dragons.InvisibleDragon.Profile values supplied by callers",
    "PackageInfo": "Dragons.InvisibleDragon.PackageInfo static class",
    "PackageInfo.NAME": "Dragons.InvisibleDragon.PackageInfo.Name (InvisibleDragon)",
    "PackageInfo.REQUIRED_PYTHON": "net48, net7.0-windows, and net8.0-windows build targets",
    "PackageInfo.VERSION": "Dragons.InvisibleDragon.PackageInfo.Version (0.1.2)",
}
RUNTIME_CONTRACTS = {
    "Directory": "class-signature:()",
    "Directory.ENERGYPLUS_DIR": "class-attribute:pathlib.Path",
    "Directory.IDD_DIR": "class-attribute:pathlib.Path",
    "Directory.PROFILE_DIR": "class-attribute:pathlib.Path",
    "PackageInfo": "class-signature:()",
    "PackageInfo.NAME": "class-attribute:str",
    "PackageInfo.REQUIRED_PYTHON": "class-attribute:tuple[int,int]",
    "PackageInfo.VERSION": "class-attribute:tuple[int,int,int]",
}

PREFIX = "constants-metadata."
CASE_SPECS = (
    ("c01-directory-import-topology", "directory", ("Directory", "Directory.ENERGYPLUS_DIR", "Directory.IDD_DIR", "Directory.PROFILE_DIR"), ()),
    ("c02-directory-two-location-relocation", "directory", ("Directory.ENERGYPLUS_DIR", "Directory.IDD_DIR", "Directory.PROFILE_DIR"), ("Directory",)),
    ("c03-directory-class-attribute-mutation", "directory", ("Directory", "Directory.ENERGYPLUS_DIR", "Directory.IDD_DIR", "Directory.PROFILE_DIR"), ()),
    ("c04-directory-instance-shadow-and-construction-errors", "directory", ("Directory",), ("Directory.ENERGYPLUS_DIR", "Directory.IDD_DIR", "Directory.PROFILE_DIR")),
    ("c05-package-info-topology-and-values", "package", ("PackageInfo", "PackageInfo.NAME", "PackageInfo.REQUIRED_PYTHON", "PackageInfo.VERSION"), ()),
    ("c06-package-info-class-attribute-mutation", "package", ("PackageInfo", "PackageInfo.NAME", "PackageInfo.REQUIRED_PYTHON", "PackageInfo.VERSION"), ()),
    ("c07-package-info-instance-shadow-and-construction-errors", "package", ("PackageInfo",), ("PackageInfo.NAME", "PackageInfo.REQUIRED_PYTHON", "PackageInfo.VERSION")),
    ("c08-package-name-string-operations", "package", ("PackageInfo.NAME",), ("PackageInfo",)),
    ("c09-package-version-tuple-operations", "package", ("PackageInfo.VERSION",), ("PackageInfo",)),
    ("c10-required-python-comparison-and-errors", "package", ("PackageInfo.REQUIRED_PYTHON",), ("PackageInfo",)),
)
EXPECTED_CASE_IDS = tuple(PREFIX + item[0] for item in CASE_SPECS)
EXPECTED_CASE_COUNT = 10

EXPECTED_FACT_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:01838d77ce2aa61318bf555e87eca84a0ce331baaec9d7334d0873a7f003b93d",
    EXPECTED_CASE_IDS[1]: "sha256:82fce5c4b885ae5d8a216d807c6e2510b156e7fdfad8e9ce90425f04592c0c5f",
    EXPECTED_CASE_IDS[2]: "sha256:4633fc76d67fc86b86dd02cb5645cf63b0d1f1b1d0685fef32e4d9921c0a8801",
    EXPECTED_CASE_IDS[3]: "sha256:310219d06e081b707886cf51be5fd0cbdffc3f71ef175cfbe93245319f5c79e1",
    EXPECTED_CASE_IDS[4]: "sha256:a12f09475a94391b4080dc1d268e3ef3f658a859c08e39e6d609a9b561ddc615",
    EXPECTED_CASE_IDS[5]: "sha256:0c68707d19661edbe03301456c008c83c2a9ca189f4e96b62f4888a9a88c5a0c",
    EXPECTED_CASE_IDS[6]: "sha256:5f1c49066b0e6e2834ebd814c4a3ec9478f6bef6a9529f95632f4e76fa66380f",
    EXPECTED_CASE_IDS[7]: "sha256:94c63c6bb305e4f6ee772888a6fa7bff0d320204a7cff7ce9c1512c58e0184ca",
    EXPECTED_CASE_IDS[8]: "sha256:faa96f7e572407ded80e4eaedf19a9b3bfdac4f595d4ff02da6cdd6871714170",
    EXPECTED_CASE_IDS[9]: "sha256:2615482afc26f694b11b990929b265d9c5bb18c1b9a376c29b3b665b22635c5b",
}
EXPECTED_CASE_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:87e43700e8ba25721a4ee21650b3624560f1d6a91fc8219884b1c97fc2f5b095",
    EXPECTED_CASE_IDS[1]: "sha256:44249e9ea23786160e587dae0f83625fbf8518eace0cefb3d2f9caa9603b2fb3",
    EXPECTED_CASE_IDS[2]: "sha256:bf9457fdb18d10c5ba8dfd269ac6d92c72dd99d473b7827b3e010e34d0ad66e7",
    EXPECTED_CASE_IDS[3]: "sha256:c364e07d87ce34695dbd16e7f508dded48c476f0cc2edb9ecb7aeb2f7c4e4873",
    EXPECTED_CASE_IDS[4]: "sha256:299cc5a1727fb2a56e2259a819c4fd3afc27265e7d85754e6e26795d72ce54b6",
    EXPECTED_CASE_IDS[5]: "sha256:b6119202c85bff43231484a0403f470cde457e759b6d8da60d1f9a5a0d2d2e4a",
    EXPECTED_CASE_IDS[6]: "sha256:687aea6edaa94ce326633a784c3e678e92ce3fe3791da88861c25a5fcdbe847a",
    EXPECTED_CASE_IDS[7]: "sha256:f3fa7c3244a09346e39ff52705dc191b57521ea8691aa83bab4e0741885ef8d7",
    EXPECTED_CASE_IDS[8]: "sha256:3f5d9a78a7a86c1b76684e025c087374a3a7452e4f700c0d6eeb4c257c1a7582",
    EXPECTED_CASE_IDS[9]: "sha256:bc1b4ff8c2e4315c865b6a0985bef3bf777a5fee5beec93d0d5c73d5a14d7e81",
}

EXPECTED_DEPENDENCIES = {
    "colorama": "0.4.6",
    "et_xmlfile": "2.0.0",
    "numpy": "2.3.1",
    "openpyxl": "3.1.5",
    "pandas": "2.3.0",
    "python-dateutil": "2.9.0.post0",
    "pytz": "2024.2",
    "six": "1.16.0",
    "tqdm": "4.67.1",
    "tzdata": "2024.2",
}
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64

PUBLIC_DIRECTORY_ATTRIBUTES = ("IDD_DIR", "PROFILE_DIR", "ENERGYPLUS_DIR")
PUBLIC_PACKAGE_ATTRIBUTES = ("NAME", "VERSION", "REQUIRED_PYTHON")
PATH_ANCHORS = {
    "IDD_DIR": ("_MODULE_ROOT", "module-root"),
    "PROFILE_DIR": ("_MODULE_ROOT", "module-root"),
    "ENERGYPLUS_DIR": ("_PACKAGE_ROOT", "package-root"),
}
ISOLATED_SOURCE_FILES = [
    "location-a/repository/src/idragon/constants.py",
    "location-b/repository/src/idragon/constants.py",
]
ISOLATED_MODULE_NAMES = [
    "_dragons_constants_metadata_location_a",
    "_dragons_constants_metadata_location_b",
]


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_schedule_type_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_constants_metadata_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load constants metadata support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("Constants metadata support is not exactly pinned.")
    return module


SUPPORT = _load_support()
strict_json_dumps = SUPPORT.strict_json_dumps
canonical_sha256 = SUPPORT.canonical_sha256
sha256_file = SUPPORT.sha256_file
load_json_without_duplicates = SUPPORT.load_json_without_duplicates
RAW_ADDRESS_PATTERN = re.compile(
    r"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])"
)
ABSOLUTE_PATH_PATTERN = re.compile(
    r"(?i)(?:[a-z]:[\\/]|\\\\[^\\]|(?<![A-Za-z0-9_.<>-])/(?:home|mnt|private|root|tmp|Users|var)(?:/|$))"
)
GUID_PATTERN = re.compile(
    r"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-"
    r"[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])"
)
TIMESTAMP_PATTERN = re.compile(
    r"(?<!\d)\d{4}-\d{2}-\d{2}[T ][0-2]\d:[0-5]\d:[0-5]\d"
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def _indexed(receipts: tuple[dict[str, Any], ...]) -> list[dict[str, Any]]:
    return [dict(item) for item in receipts]


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    if path.stat().st_size != EXPECTED_INVENTORY_FILE_BYTES:
        raise SystemExit("The public-symbol inventory byte length is not pinned.")
    if sha256_file(path) != EXPECTED_INVENTORY_FILE_SHA256:
        raise SystemExit("The public-symbol inventory file hash is not pinned.")
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(SUPPORT, name) for name in names}
    all_symbols = tuple(item["symbol"] for item in ALL_RECEIPTS)
    try:
        SUPPORT.SOURCE_PATH = SOURCE_PATH
        SUPPORT.EXPECTED_SOURCE_SHA256 = EXPECTED_SOURCE_SHA256
        SUPPORT.EXPECTED_SYMBOL_HASHES = {
            item["symbol"]: item["symbol_hash"] for item in ALL_RECEIPTS
        }
        SUPPORT.TARGET_SYMBOLS = all_symbols
        loaded = SUPPORT.load_exact_inventory(path, upstream_commit)
    finally:
        for name, value in original.items():
            setattr(SUPPORT, name, value)

    expected_file = {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }
    expected_symbols = [_descriptor(item) for item in ALL_RECEIPTS]
    if loaded["file"] != expected_file or loaded["symbols"] != expected_symbols:
        raise SystemExit("The constants metadata inventory receipts are not exact.")
    return {
        "content_sha256": loaded["content_sha256"],
        "file": loaded["file"],
        "resolved_receipts": _indexed(RESOLVED_RECEIPTS),
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": _indexed(TARGET_RECEIPTS),
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = tuple(
        {
            "context_symbols": list(context_symbols),
            "id": PREFIX + suffix,
            "subfamily": subfamily,
            "target_symbols": list(target_symbols),
        }
        for suffix, subfamily, target_symbols, context_symbols in CASE_SPECS
    )
    if tuple(item["id"] for item in definitions) != EXPECTED_CASE_IDS:
        raise RuntimeError("Constants metadata case IDs drifted.")
    target_counts = Counter(
        symbol for item in definitions for symbol in item["target_symbols"]
    )
    if set(target_counts) != set(TARGET_SYMBOLS) or len(definitions) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Constants metadata target coverage is incomplete.")
    permitted = set(TARGET_SYMBOLS)
    if any(
        not set(item["context_symbols"]).issubset(permitted)
        for item in definitions
    ):
        raise RuntimeError("Constants metadata case context escaped the bounded surface.")
    if set(RESOLVED_SYMBOLS).intersection(target_counts):
        raise RuntimeError("Resolved constants symbols were retargeted.")
    return definitions


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


def _find_pinned_source() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        candidate = Path(entry) / "idragon" / "constants.py"
        if (
            candidate.is_file()
            and candidate.stat().st_size == EXPECTED_SOURCE_BYTES
            and sha256_file(candidate) == EXPECTED_SOURCE_SHA256
        ):
            matches.append(candidate.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon/constants.py must be importable.")
    return unique[0]


def _load_isolated_module(source: Path, destination: Path, name: str) -> ModuleType:
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    if (
        destination.stat().st_size != EXPECTED_SOURCE_BYTES
        or sha256_file(destination) != EXPECTED_SOURCE_SHA256
    ):
        raise SystemExit("An isolated constants.py copy is not byte-identical.")
    spec = importlib.util.spec_from_file_location(name, destination)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot import isolated constants source: {name}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    try:
        spec.loader.exec_module(module)
    except BaseException:
        sys.modules.pop(name, None)
        raise
    if Path(module.__file__).resolve() != destination.resolve():
        raise SystemExit("The isolated constants module resolved to another file.")
    return module


@contextmanager
def _isolated_modules(source: Path) -> Iterator[tuple[Path, ModuleType, ModuleType]]:
    with tempfile.TemporaryDirectory(prefix="constants-metadata-oracle-") as temporary:
        # ``Path(__file__).resolve()`` expands Windows 8.3 segments.  Resolve the
        # isolation anchor too so every relative observation compares like with
        # like while the absolute value itself remains outside the fixture.
        root = Path(temporary).resolve()
        paths = [root / Path(item) for item in ISOLATED_SOURCE_FILES]
        modules: list[ModuleType] = []
        try:
            for path, name in zip(paths, ISOLATED_MODULE_NAMES, strict=True):
                modules.append(_load_isolated_module(source, path, name))
            yield root, modules[0], modules[1]
        finally:
            for name in ISOLATED_MODULE_NAMES:
                sys.modules.pop(name, None)


def _encode(value: Any) -> dict[str, Any]:
    if value is None:
        return {"kind": "none"}
    if type(value) is bool:
        return {"kind": "bool", "value": value}
    if type(value) is int:
        return {"kind": "int", "value": str(value)}
    if type(value) is str:
        return {"kind": "str", "value": value}
    if type(value) is tuple:
        return {"items": [_encode(item) for item in value], "kind": "tuple"}
    if type(value) is list:
        return {"items": [_encode(item) for item in value], "kind": "list"}
    raise RuntimeError(f"Unsupported constants metadata value: {type(value).__name__}")


def _portable_type_name(value: Any) -> str:
    if isinstance(value, Path):
        return "Path"
    return type(value).__name__


def _event(
    phase: str, operation: Callable[[], Any]
) -> tuple[dict[str, Any], Any | None]:
    try:
        result = operation()
    except Exception as error:  # Exact error timing is oracle evidence.
        return (
            {
                "error": {"message": str(error), "type": type(error).__name__},
                "outcome": "raised",
                "phase": phase,
            },
            None,
        )
    return (
        {
            "outcome": "returned",
            "phase": phase,
            "return_type": _portable_type_name(result),
            "returned_none": result is None,
        },
        result,
    )


def _relative_path(value: Path, anchor: Path) -> list[str]:
    return list(value.relative_to(anchor).parts)


def _directory_value(directory: type[Any], name: str, value: Any) -> dict[str, Any]:
    if not isinstance(value, Path):
        return _encode(value)
    if not value.is_absolute():
        return {
            "is_absolute": False,
            "kind": "path",
            "parts": list(value.parts),
        }
    anchor_name, label = PATH_ANCHORS[name]
    anchor = getattr(directory, anchor_name)
    return {
        "anchor": label,
        "exists": value.exists(),
        "is_absolute": True,
        "is_dir": value.is_dir(),
        "is_file": value.is_file(),
        "kind": "path",
        "relative_parts": _relative_path(value, anchor),
    }


def _directory_state(directory: type[Any]) -> dict[str, Any]:
    state: dict[str, Any] = {}
    namespace = directory.__dict__
    for name in PUBLIC_DIRECTORY_ATTRIBUTES:
        if name not in namespace:
            state[name] = {"present": False}
        else:
            state[name] = {
                "present": True,
                "value": _directory_value(directory, name, namespace[name]),
            }
    return state


def _directory_anchor_state(
    module: ModuleType, location_root: Path
) -> dict[str, Any]:
    directory = module.Directory
    values = {
        "_DATA_DIR": directory._DATA_DIR,
        "_MODULE_ROOT": directory._MODULE_ROOT,
        "_PACKAGE_ROOT": directory._PACKAGE_ROOT,
    }
    return {
        name: {
            "exists": value.exists(),
            "is_absolute": value.is_absolute(),
            "relative_to_isolated_location": _relative_path(value, location_root),
        }
        for name, value in values.items()
    }


def _package_state(package: type[Any]) -> dict[str, Any]:
    state: dict[str, Any] = {}
    namespace = package.__dict__
    for name in PUBLIC_PACKAGE_ATTRIBUTES:
        if name not in namespace:
            state[name] = {"present": False}
        else:
            state[name] = {"present": True, "value": _encode(namespace[name])}
    return state


def _runtime_contracts(module: ModuleType) -> dict[str, str]:
    directory = module.Directory
    package = module.PackageInfo
    contracts = {
        "Directory": "class-signature:" + str(inspect.signature(directory)),
        "Directory.ENERGYPLUS_DIR": "class-attribute:pathlib.Path" if isinstance(directory.ENERGYPLUS_DIR, Path) else "invalid",
        "Directory.IDD_DIR": "class-attribute:pathlib.Path" if isinstance(directory.IDD_DIR, Path) else "invalid",
        "Directory.PROFILE_DIR": "class-attribute:pathlib.Path" if isinstance(directory.PROFILE_DIR, Path) else "invalid",
        "PackageInfo": "class-signature:" + str(inspect.signature(package)),
        "PackageInfo.NAME": "class-attribute:" + type(package.NAME).__name__,
        "PackageInfo.REQUIRED_PYTHON": "class-attribute:tuple[int,int]" if type(package.REQUIRED_PYTHON) is tuple and all(type(item) is int for item in package.REQUIRED_PYTHON) and len(package.REQUIRED_PYTHON) == 2 else "invalid",
        "PackageInfo.VERSION": "class-attribute:tuple[int,int,int]" if type(package.VERSION) is tuple and all(type(item) is int for item in package.VERSION) and len(package.VERSION) == 3 else "invalid",
    }
    return contracts


def _facts(
    scenario: str,
    observations: dict[str, Any],
    snapshots: list[dict[str, Any]],
    events: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "events": events,
        "observations": observations,
        "scenario": scenario,
        "source_state": {"snapshots": snapshots},
    }


def _execute_c01(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, Any]:
    del module_b, root_b
    directory = module_a.Directory
    before = _directory_state(directory)
    events = []
    event, public_names = _event(
        "read-public-class-namespace",
        lambda: [name for name in directory.__dict__ if name in PUBLIC_DIRECTORY_ATTRIBUTES],
    )
    events.append(event)
    event, values = _event(
        "read-three-public-paths",
        lambda: tuple(getattr(directory, name) for name in PUBLIC_DIRECTORY_ATTRIBUTES),
    )
    events.append(event)
    observations = {
        "anchor_state": _directory_anchor_state(module_a, root_a),
        "base_names": [base.__name__ for base in directory.__bases__],
        "class_name": directory.__name__,
        "public_member_names": public_names,
        "repeated_read_identity": {
            name: getattr(directory, name) is getattr(directory, name)
            for name in PUBLIC_DIRECTORY_ATTRIBUTES
        },
        "signature": str(inspect.signature(directory)),
        "target_values": {
            name: _directory_value(directory, name, value)
            for name, value in zip(PUBLIC_DIRECTORY_ATTRIBUTES, values, strict=True)
        },
    }
    return _facts(
        "C01",
        observations,
        [
            {"phase": "before", "state": before},
            {"phase": "after", "state": _directory_state(directory)},
        ],
        events,
    )


def _execute_c02(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, Any]:
    events = []
    event, pairs = _event(
        "read-path-pairs",
        lambda: tuple(
            (getattr(module_a.Directory, name), getattr(module_b.Directory, name))
            for name in PUBLIC_DIRECTORY_ATTRIBUTES
        ),
    )
    events.append(event)
    observations = {
        "directory_class_identity": module_a.Directory is module_b.Directory,
        "directory_class_names": [module_a.Directory.__name__, module_b.Directory.__name__],
        "path_pairs": {
            name: {
                "location_a": _directory_value(module_a.Directory, name, left),
                "location_b": _directory_value(module_b.Directory, name, right),
                "location_a_equals_location_b": left == right,
                "location_a_is_location_b": left is right,
            }
            for name, (left, right) in zip(PUBLIC_DIRECTORY_ATTRIBUTES, pairs, strict=True)
        },
    }
    snapshots = [
        {
            "anchor_state": _directory_anchor_state(module_a, root_a),
            "phase": "location-a",
            "state": _directory_state(module_a.Directory),
        },
        {
            "anchor_state": _directory_anchor_state(module_b, root_b),
            "phase": "location-b",
            "state": _directory_state(module_b.Directory),
        },
    ]
    return _facts("C02", observations, snapshots, events)


def _execute_c03(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, Any]:
    del module_b, root_a, root_b
    directory = module_a.Directory
    originals = {name: getattr(directory, name) for name in PUBLIC_DIRECTORY_ATTRIBUTES}
    events: list[dict[str, Any]] = []
    snapshots: list[dict[str, Any]] = [
        {"phase": "before", "state": _directory_state(directory)}
    ]
    try:
        for name in PUBLIC_DIRECTORY_ATTRIBUTES:
            assigned = Path("relative-probe") / name.lower()
            event, _ = _event(
                f"assign-{name}", lambda name=name, assigned=assigned: setattr(directory, name, assigned)
            )
            events.append(event)
            snapshots.append({"phase": f"after-assign-{name}", "state": _directory_state(directory)})
            event, _ = _event(f"delete-{name}", lambda name=name: delattr(directory, name))
            events.append(event)
            snapshots.append({"phase": f"after-delete-{name}", "state": _directory_state(directory)})
            event, _ = _event(f"read-missing-{name}", lambda name=name: getattr(directory, name))
            events.append(event)
            event, _ = _event(
                f"restore-{name}", lambda name=name: setattr(directory, name, originals[name])
            )
            events.append(event)
            snapshots.append({"phase": f"after-restore-{name}", "state": _directory_state(directory)})
    finally:
        for name, value in originals.items():
            setattr(directory, name, value)
    observations = {
        "restored_object_identity": {
            name: getattr(directory, name) is value for name, value in originals.items()
        }
    }
    snapshots.append({"phase": "finally", "state": _directory_state(directory)})
    return _facts("C03", observations, snapshots, events)


def _execute_c04(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, Any]:
    del module_b, root_a, root_b
    directory = module_a.Directory
    before = _directory_state(directory)
    events: list[dict[str, Any]] = []
    event, first = _event("construct-first", directory)
    events.append(event)
    event, second = _event("construct-second", directory)
    events.append(event)
    observations: dict[str, Any] = {
        "first_instance_dictionary_before": dict(first.__dict__),
        "first_is_second": first is second,
        "inherited_attributes": {},
        "second_instance_dictionary": dict(second.__dict__),
    }
    for name in PUBLIC_DIRECTORY_ATTRIBUTES:
        owned_before = name in first.__dict__
        inherited = getattr(first, name)
        assigned = Path("instance-probe") / name.lower()
        event, _ = _event(
            f"assign-instance-{name}", lambda name=name, assigned=assigned: setattr(first, name, assigned)
        )
        events.append(event)
        assigned_value = getattr(first, name)
        owned_after_assignment = name in first.__dict__
        event, _ = _event(f"delete-instance-{name}", lambda name=name: delattr(first, name))
        events.append(event)
        observations["inherited_attributes"][name] = {
            "after_delete_is_class_value": getattr(first, name) is getattr(directory, name),
            "assigned_value": _directory_value(directory, name, assigned_value),
            "before_is_class_value": inherited is getattr(directory, name),
            "owned_after_delete": name in first.__dict__,
            "owned_after_assignment": owned_after_assignment,
            "owned_before": owned_before,
        }
    event, _ = _event("construct-positional-error", lambda: directory(1))
    events.append(event)
    event, _ = _event("construct-keyword-error", lambda: directory(value=1))
    events.append(event)
    observations["first_instance_dictionary_after"] = dict(first.__dict__)
    return _facts(
        "C04",
        observations,
        [
            {"phase": "before", "state": before},
            {"phase": "after", "state": _directory_state(directory)},
        ],
        events,
    )


def _execute_c05(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, Any]:
    del module_b, root_a, root_b
    package = module_a.PackageInfo
    before = _package_state(package)
    events = []
    event, public_names = _event(
        "read-public-class-namespace",
        lambda: [name for name in package.__dict__ if name in PUBLIC_PACKAGE_ATTRIBUTES],
    )
    events.append(event)
    event, first = _event("construct-first", package)
    events.append(event)
    event, second = _event("construct-second", package)
    events.append(event)
    observations = {
        "base_names": [base.__name__ for base in package.__bases__],
        "class_name": package.__name__,
        "first_is_second": first is second,
        "public_member_names": public_names,
        "repeated_read_identity": {
            name: getattr(package, name) is getattr(package, name)
            for name in PUBLIC_PACKAGE_ATTRIBUTES
        },
        "signature": str(inspect.signature(package)),
        "target_values": {
            name: _encode(getattr(package, name)) for name in PUBLIC_PACKAGE_ATTRIBUTES
        },
    }
    return _facts(
        "C05",
        observations,
        [
            {"phase": "before", "state": before},
            {"phase": "after", "state": _package_state(package)},
        ],
        events,
    )


def _execute_c06(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, Any]:
    del module_b, root_a, root_b
    package = module_a.PackageInfo
    originals = {name: getattr(package, name) for name in PUBLIC_PACKAGE_ATTRIBUTES}
    replacements = {"NAME": None, "VERSION": "0.7.0", "REQUIRED_PYTHON": [3, 12]}
    events: list[dict[str, Any]] = []
    snapshots: list[dict[str, Any]] = [
        {"phase": "before", "state": _package_state(package)}
    ]
    try:
        for name in PUBLIC_PACKAGE_ATTRIBUTES:
            event, _ = _event(
                f"assign-{name}", lambda name=name: setattr(package, name, replacements[name])
            )
            events.append(event)
            snapshots.append({"phase": f"after-assign-{name}", "state": _package_state(package)})
            event, _ = _event(f"delete-{name}", lambda name=name: delattr(package, name))
            events.append(event)
            snapshots.append({"phase": f"after-delete-{name}", "state": _package_state(package)})
            event, _ = _event(f"read-missing-{name}", lambda name=name: getattr(package, name))
            events.append(event)
            event, _ = _event(
                f"restore-{name}", lambda name=name: setattr(package, name, originals[name])
            )
            events.append(event)
            snapshots.append({"phase": f"after-restore-{name}", "state": _package_state(package)})
    finally:
        for name, value in originals.items():
            setattr(package, name, value)
    observations = {
        "restored_object_identity": {
            name: getattr(package, name) is value for name, value in originals.items()
        }
    }
    snapshots.append({"phase": "finally", "state": _package_state(package)})
    return _facts("C06", observations, snapshots, events)


def _execute_c07(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, Any]:
    del module_b, root_a, root_b
    package = module_a.PackageInfo
    before = _package_state(package)
    events: list[dict[str, Any]] = []
    event, first = _event("construct-first", package)
    events.append(event)
    event, second = _event("construct-second", package)
    events.append(event)
    replacements = {"NAME": "instance-name", "VERSION": (9,), "REQUIRED_PYTHON": (2,)}
    observations: dict[str, Any] = {
        "first_instance_dictionary_before": dict(first.__dict__),
        "first_is_second": first is second,
        "inherited_attributes": {},
        "second_instance_dictionary": dict(second.__dict__),
    }
    for name in PUBLIC_PACKAGE_ATTRIBUTES:
        owned_before = name in first.__dict__
        inherited = getattr(first, name)
        event, _ = _event(
            f"assign-instance-{name}", lambda name=name: setattr(first, name, replacements[name])
        )
        events.append(event)
        assigned_value = getattr(first, name)
        owned_after_assignment = name in first.__dict__
        event, _ = _event(f"delete-instance-{name}", lambda name=name: delattr(first, name))
        events.append(event)
        observations["inherited_attributes"][name] = {
            "after_delete_is_class_value": getattr(first, name) is getattr(package, name),
            "assigned_value": _encode(assigned_value),
            "before_is_class_value": inherited is getattr(package, name),
            "owned_after_assignment": owned_after_assignment,
            "owned_after_delete": name in first.__dict__,
            "owned_before": owned_before,
        }
    event, _ = _event("construct-positional-error", lambda: package(1))
    events.append(event)
    event, _ = _event("construct-keyword-error", lambda: package(value=1))
    events.append(event)
    observations["first_instance_dictionary_after"] = dict(first.__dict__)
    return _facts(
        "C07",
        observations,
        [
            {"phase": "before", "state": before},
            {"phase": "after", "state": _package_state(package)},
        ],
        events,
    )


def _execute_operations(
    operations: tuple[tuple[str, Callable[[], Any]], ...]
) -> tuple[list[dict[str, Any]], dict[str, dict[str, Any]]]:
    events: list[dict[str, Any]] = []
    returned: dict[str, dict[str, Any]] = {}
    for phase, operation in operations:
        event, result = _event(phase, operation)
        events.append(event)
        if event["outcome"] == "returned":
            returned[phase] = _encode(result)
    return events, returned


def _execute_c08(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, Any]:
    del module_b, root_a, root_b
    package = module_a.PackageInfo
    name = package.NAME

    def assign_item() -> None:
        name[0] = "I"  # type: ignore[index]

    events, returned = _execute_operations(
        (
            ("length", lambda: len(name)),
            ("split-hyphen", lambda: name.split("-")),
            ("upper", name.upper),
            ("placeholder-index-7", lambda: f"##{name}{7:04d}##"),
            ("temporary-prefix", lambda: name + "-"),
            ("slice-first-nine", lambda: name[:9]),
            ("assign-index-error", assign_item),
        )
    )
    return _facts(
        "C08",
        {"operation_results": returned},
        [
            {"phase": "before", "state": _package_state(package)},
            {"phase": "after", "state": _package_state(package)},
        ],
        events,
    )


def _execute_c09(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, Any]:
    del module_b, root_a, root_b
    package = module_a.PackageInfo
    version = package.VERSION

    def assign_item() -> None:
        version[0] = 1  # type: ignore[index]

    events, returned = _execute_operations(
        (
            ("length", lambda: len(version)),
            ("index-zero", lambda: version[0]),
            ("index-negative-one", lambda: version[-1]),
            ("slice-first-two", lambda: version[:2]),
            ("display-join", lambda: ".".join(str(item) for item in version)),
            ("less-than-0.7.1", lambda: version < (0, 7, 1)),
            ("greater-than-0.6.9", lambda: version > (0, 6, 9)),
            ("equals-list", lambda: version == [0, 7, 0]),
            ("concatenate-patch", lambda: version + (1,)),
            ("assign-index-error", assign_item),
        )
    )
    return _facts(
        "C09",
        {"operation_results": returned},
        [
            {"phase": "before", "state": _package_state(package)},
            {"phase": "after", "state": _package_state(package)},
        ],
        events,
    )


def _execute_c10(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, Any]:
    del module_b, root_a, root_b
    package = module_a.PackageInfo
    required = package.REQUIRED_PYTHON
    probes = ((3, 11), (3, 12), (3, 12, 0), (4, 0))

    def assign_item() -> None:
        required[0] = 2  # type: ignore[index]

    operations: list[tuple[str, Callable[[], Any]]] = [
        ("pinned-runtime-less-than-required", lambda: sys.version_info < required),
        ("direct-comma-join-error", lambda: ",".join(required)),  # type: ignore[arg-type]
        ("stringified-comma-join", lambda: ",".join(str(item) for item in required)),
        ("concatenate-patch", lambda: required + (0,)),
        ("assign-index-error", assign_item),
    ]
    for probe in probes:
        label = "probe-" + ".".join(map(str, probe)) + "-less-than-required"
        operations.append((label, lambda probe=probe: probe < required))
    events, returned = _execute_operations(tuple(operations))
    observations = {
        "operation_results": returned,
        "pinned_runtime_version": _encode(tuple(sys.version_info[:3])),
        "probe_inputs": [_encode(probe) for probe in probes],
    }
    return _facts(
        "C10",
        observations,
        [
            {"phase": "before", "state": _package_state(package)},
            {"phase": "after", "state": _package_state(package)},
        ],
        events,
    )


EXECUTORS = {
    EXPECTED_CASE_IDS[0]: _execute_c01,
    EXPECTED_CASE_IDS[1]: _execute_c02,
    EXPECTED_CASE_IDS[2]: _execute_c03,
    EXPECTED_CASE_IDS[3]: _execute_c04,
    EXPECTED_CASE_IDS[4]: _execute_c05,
    EXPECTED_CASE_IDS[5]: _execute_c06,
    EXPECTED_CASE_IDS[6]: _execute_c07,
    EXPECTED_CASE_IDS[7]: _execute_c08,
    EXPECTED_CASE_IDS[8]: _execute_c09,
    EXPECTED_CASE_IDS[9]: _execute_c10,
}


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def case_sha256(cases: list[dict[str, Any]]) -> dict[str, str]:
    return {case["id"]: canonical_sha256(case) for case in cases}


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


def _expected_upstream() -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory": {
            "bytes": EXPECTED_INVENTORY_FILE_BYTES,
            "content_sha256": EXPECTED_INVENTORY_SHA256,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        },
        "isolated_import": {
            "files_after_execution": ISOLATED_SOURCE_FILES,
            "module_names": ISOLATED_MODULE_NAMES,
            "source_copy_sha256": {
                item: EXPECTED_SOURCE_SHA256 for item in ISOLATED_SOURCE_FILES
            },
        },
        "source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "path": SOURCE_PATH,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
    }


def _expected_consumer_contract() -> dict[str, Any]:
    unresolved = [
        "imports whose loader supplies no real __file__, including frozen and zip imports",
        "symlink or junction topology and resolve behavior outside the two ordinary isolated locations",
        "POSIX, drive-relative, UNC, alternate-drive, and case-folding path flavor differences",
        "filesystem permission failures and populated, missing, or concurrently replaced resource directories",
        "concurrent class mutation, custom metaclasses, descriptors, and arbitrary replacement objects beyond the recorded probes",
        "PackageInfo tuple consumer behavior for non-integer, non-finite, negative, huge, or mixed replacement members",
        "Python interpreter versions and implementations other than exact CPython 3.12.7",
        "native cross-language behavior until a separately pinned C# binding executes these adaptation candidates",
    ]
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_counts": {"equivalent": 0, "exception": 8},
        "classifications": CLASSIFICATIONS,
        "closure": {
            "full_symbol_closure": False,
            "private_context_members_observed": [
                "Directory._DATA_DIR",
                "Directory._MODULE_ROOT",
                "Directory._PACKAGE_ROOT",
            ],
            "resolved_receipts_not_retargeted": _indexed(RESOLVED_RECEIPTS),
            "target_coverage_complete": True,
            "unresolved_boundaries": unresolved,
        },
        "native_adaptation_candidates": NATIVE_ADAPTATION_CANDIDATES,
        "native_binding_status": "proposed-not-yet-cross-language-verified",
        "path_encoding": "anchor-relative-parts-only-no-host-absolute-paths",
        "runtime_contracts": RUNTIME_CONTRACTS,
        "target_symbols": list(TARGET_SYMBOLS),
    }


def _require_keys(value: Any, expected: set[str], location: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(f"{location} key set drifted: {actual!r}")


def _validate_encoded(value: dict[str, Any], location: str) -> bool:
    kind = value.get("kind")
    if kind == "none":
        _require_keys(value, {"kind"}, location)
        return True
    if kind == "bool":
        _require_keys(value, {"kind", "value"}, location)
        if type(value["value"]) is not bool:
            raise RuntimeError(f"Invalid encoded bool at {location}.")
        return True
    if kind == "int":
        _require_keys(value, {"kind", "value"}, location)
        try:
            if str(int(value["value"])) != value["value"]:
                raise ValueError
        except (TypeError, ValueError) as error:
            raise RuntimeError(f"Invalid encoded int at {location}.") from error
        return True
    if kind == "str":
        _require_keys(value, {"kind", "value"}, location)
        if type(value["value"]) is not str:
            raise RuntimeError(f"Invalid encoded str at {location}.")
        return True
    if kind in {"list", "tuple"}:
        _require_keys(value, {"items", "kind"}, location)
        if not isinstance(value["items"], list):
            raise RuntimeError(f"Invalid encoded sequence at {location}.")
        for index, item in enumerate(value["items"]):
            if not isinstance(item, dict) or not _validate_encoded(item, f"{location}[{index}]"):
                raise RuntimeError(f"Invalid encoded item at {location}[{index}].")
        return True
    return False


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if isinstance(value, float):
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
        if "kind" in value and _validate_encoded(value, location):
            return
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"Non-string key at {location}.")
            _validate_safe_tree(key, f"{location}.<key>")
            _validate_safe_tree(item, f"{location}.{key}")
        return
    raise RuntimeError(f"Unsupported raw value at {location}: {type(value).__name__}")


def _case_by_scenario(value: dict[str, Any], scenario: str) -> dict[str, Any]:
    matches = [case for case in value["cases"] if case["python"]["facts"]["scenario"] == scenario]
    if len(matches) != 1:
        raise RuntimeError(f"Expected one constants metadata scenario {scenario}.")
    return matches[0]


def _validate_case_facts(identifier: str, facts: dict[str, Any]) -> None:
    expected_hash = EXPECTED_FACT_SHA256.get(identifier)
    if expected_hash is not None and canonical_sha256(facts) != expected_hash:
        raise RuntimeError(f"Constants metadata canonical semantics drifted: {identifier}")
    _require_keys(
        facts,
        {"events", "observations", "scenario", "source_state"},
        f"facts {identifier}",
    )
    expected_scenario = identifier.removeprefix(PREFIX)[:3].upper()
    if facts["scenario"] != expected_scenario:
        raise RuntimeError(f"Constants metadata scenario label drifted: {identifier}")
    _require_keys(facts["source_state"], {"snapshots"}, f"state {identifier}")
    if not isinstance(facts["source_state"]["snapshots"], list):
        raise RuntimeError(f"Constants metadata snapshots drifted: {identifier}")
    if not isinstance(facts["events"], list) or not facts["events"]:
        raise RuntimeError(f"Constants metadata events drifted: {identifier}")
    for event in facts["events"]:
        if not isinstance(event.get("phase"), str) or event.get("outcome") not in {"raised", "returned"}:
            raise RuntimeError(f"Constants metadata event drifted: {identifier}")
        if event["outcome"] == "raised":
            _require_keys(event, {"error", "outcome", "phase"}, f"error event {identifier}")
            _require_keys(event["error"], {"message", "type"}, f"error {identifier}")
        else:
            _require_keys(event, {"outcome", "phase", "return_type", "returned_none"}, f"return event {identifier}")

    observations = facts["observations"]
    scenario = facts["scenario"]
    if scenario == "C01":
        valid = (
            observations["public_member_names"] == ["IDD_DIR", "PROFILE_DIR", "ENERGYPLUS_DIR"]
            and observations["signature"] == "()"
            and observations["target_values"]["IDD_DIR"]["relative_parts"] == ["_data", "idd"]
            and observations["target_values"]["PROFILE_DIR"]["relative_parts"] == ["_data", "profile"]
            and observations["target_values"]["ENERGYPLUS_DIR"]["relative_parts"] == ["runtime"]
            and all(observations["repeated_read_identity"].values())
        )
    elif scenario == "C02":
        valid = (
            observations["directory_class_identity"] is False
            and all(not item["location_a_equals_location_b"] and not item["location_a_is_location_b"] for item in observations["path_pairs"].values())
            and all(item["location_a"]["relative_parts"] == item["location_b"]["relative_parts"] for item in observations["path_pairs"].values())
        )
    elif scenario == "C03":
        errors = [event for event in facts["events"] if event["outcome"] == "raised"]
        expected_errors = [
            {
                "message": f"type object 'Directory' has no attribute '{name}'",
                "type": "AttributeError",
            }
            for name in PUBLIC_DIRECTORY_ATTRIBUTES
        ]
        valid = (
            [event["error"] for event in errors] == expected_errors
            and all(observations["restored_object_identity"].values())
            and facts["source_state"]["snapshots"][0]["state"] == facts["source_state"]["snapshots"][-1]["state"]
        )
    elif scenario == "C04":
        errors = [event for event in facts["events"] if event["outcome"] == "raised"]
        valid = (
            observations["first_is_second"] is False
            and observations["first_instance_dictionary_before"] == {}
            and observations["first_instance_dictionary_after"] == {}
            and len(errors) == 2
            and all(event["error"] == {"message": "Directory() takes no arguments", "type": "TypeError"} for event in errors)
            and all(item["before_is_class_value"] and item["after_delete_is_class_value"] and not item["owned_before"] and item["owned_after_assignment"] and not item["owned_after_delete"] for item in observations["inherited_attributes"].values())
            and facts["source_state"]["snapshots"][0]["state"] == facts["source_state"]["snapshots"][1]["state"]
        )
    elif scenario == "C05":
        valid = (
            observations["public_member_names"] == ["NAME", "VERSION", "REQUIRED_PYTHON"]
            and observations["signature"] == "()"
            and observations["target_values"]["NAME"] == _encode("invisible-dragon")
            and observations["target_values"]["VERSION"] == _encode((0, 7, 0))
            and observations["target_values"]["REQUIRED_PYTHON"] == _encode((3, 12))
            and all(observations["repeated_read_identity"].values())
        )
    elif scenario == "C06":
        errors = [event for event in facts["events"] if event["outcome"] == "raised"]
        expected_errors = [
            {
                "message": f"type object 'PackageInfo' has no attribute '{name}'",
                "type": "AttributeError",
            }
            for name in PUBLIC_PACKAGE_ATTRIBUTES
        ]
        valid = (
            [event["error"] for event in errors] == expected_errors
            and all(observations["restored_object_identity"].values())
            and facts["source_state"]["snapshots"][0]["state"] == facts["source_state"]["snapshots"][-1]["state"]
        )
    elif scenario == "C07":
        errors = [event for event in facts["events"] if event["outcome"] == "raised"]
        valid = (
            observations["first_is_second"] is False
            and observations["first_instance_dictionary_before"] == {}
            and observations["first_instance_dictionary_after"] == {}
            and len(errors) == 2
            and all(event["error"] == {"message": "PackageInfo() takes no arguments", "type": "TypeError"} for event in errors)
            and all(item["before_is_class_value"] and item["after_delete_is_class_value"] and not item["owned_before"] and item["owned_after_assignment"] and not item["owned_after_delete"] for item in observations["inherited_attributes"].values())
        )
    elif scenario == "C08":
        results = observations["operation_results"]
        error = facts["events"][-1]
        valid = (
            results["length"] == _encode(16)
            and results["split-hyphen"] == _encode(["invisible", "dragon"])
            and results["upper"] == _encode("INVISIBLE-DRAGON")
            and results["placeholder-index-7"] == _encode("##invisible-dragon0007##")
            and results["temporary-prefix"] == _encode("invisible-dragon-")
            and results["slice-first-nine"] == _encode("invisible")
            and error["error"] == {"message": "'str' object does not support item assignment", "type": "TypeError"}
        )
    elif scenario == "C09":
        results = observations["operation_results"]
        error = facts["events"][-1]
        valid = (
            results["length"] == _encode(3)
            and results["slice-first-two"] == _encode((0, 7))
            and results["display-join"] == _encode("0.7.0")
            and results["less-than-0.7.1"] == _encode(True)
            and results["greater-than-0.6.9"] == _encode(True)
            and results["equals-list"] == _encode(False)
            and results["concatenate-patch"] == _encode((0, 7, 0, 1))
            and error["error"] == {"message": "'tuple' object does not support item assignment", "type": "TypeError"}
        )
    elif scenario == "C10":
        results = observations["operation_results"]
        errors = [event for event in facts["events"] if event["outcome"] == "raised"]
        valid = (
            observations["pinned_runtime_version"] == _encode((3, 12, 7))
            and results["pinned-runtime-less-than-required"] == _encode(False)
            and results["stringified-comma-join"] == _encode("3,12")
            and results["concatenate-patch"] == _encode((3, 12, 0))
            and results["probe-3.11-less-than-required"] == _encode(True)
            and results["probe-3.12-less-than-required"] == _encode(False)
            and results["probe-3.12.0-less-than-required"] == _encode(False)
            and results["probe-4.0-less-than-required"] == _encode(False)
            and [event["error"] for event in errors] == [
                {"message": "sequence item 0: expected str instance, int found", "type": "TypeError"},
                {"message": "'tuple' object does not support item assignment", "type": "TypeError"},
            ]
        )
    else:
        valid = False
    if not valid:
        raise RuntimeError(f"Constants metadata semantic invariant drifted: {identifier}")


def build_oracle(
    inventory: dict[str, Any], commit: str, source: Path | None = None
) -> dict[str, Any]:
    imported_source = source.resolve() if source is not None else _find_pinned_source()
    if (
        imported_source.stat().st_size != EXPECTED_SOURCE_BYTES
        or sha256_file(imported_source) != EXPECTED_SOURCE_SHA256
    ):
        raise SystemExit("The imported constants.py source is not exactly pinned.")
    expected_inventory = {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "file": {
            "ast_hash": EXPECTED_SOURCE_AST_SHA256,
            "content_hash": EXPECTED_SOURCE_SHA256,
            "path": SOURCE_PATH,
        },
        "resolved_receipts": _indexed(RESOLVED_RECEIPTS),
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": _indexed(TARGET_RECEIPTS),
    }
    if inventory != expected_inventory:
        raise SystemExit("The aggregate constants metadata inventory is not exact.")

    with _isolated_modules(imported_source) as (isolated_root, module_a, module_b):
        if _runtime_contracts(module_a) != RUNTIME_CONTRACTS or _runtime_contracts(module_b) != RUNTIME_CONTRACTS:
            raise SystemExit("Pinned constants metadata runtime contracts drifted.")
        root_a = isolated_root / "location-a"
        root_b = isolated_root / "location-b"
        observed = {
            definition["id"]: EXECUTORS[definition["id"]](module_a, module_b, root_a, root_b)
            for definition in case_definitions()
        }
        fact_hashes = {identifier: canonical_sha256(facts) for identifier, facts in observed.items()}
        if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
            raise SystemExit(
                "Pinned constants metadata per-case facts drifted.\nOBSERVED_FACT_HASHES\n"
                + strict_json_dumps(fact_hashes, indent=2)
            )
        cases: list[dict[str, Any]] = []
        for definition in case_definitions():
            identifier = definition["id"]
            facts = observed[identifier]
            _validate_case_facts(identifier, facts)
            case = dict(definition)
            case["python"] = {
                "facts": facts,
                "facts_sha256": fact_hashes[identifier],
                "outcome": "observed",
            }
            cases.append(case)
        case_hashes = case_sha256(cases)
        if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
            raise SystemExit(
                "Pinned constants metadata per-case records drifted.\nOBSERVED_CASE_HASHES\n"
                + strict_json_dumps(case_hashes, indent=2)
            )
        files_after = sorted(
            path.relative_to(isolated_root).as_posix()
            for path in isolated_root.rglob("*")
            if path.is_file()
        )
        source_copy_hashes = {
            item: sha256_file(isolated_root / Path(item)) for item in files_after
        }
        upstream = _expected_upstream()
        upstream["isolated_import"] = {
            "files_after_execution": files_after,
            "module_names": list(ISOLATED_MODULE_NAMES),
            "source_copy_sha256": source_copy_hashes,
        }
        result = {
            "case_sha256": case_hashes,
            "cases": cases,
            "cases_sha256": cases_sha256(cases),
            "consumer_contract": _expected_consumer_contract(),
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
            "upstream": upstream,
        }
    validate_oracle(result)
    return result


def validate_oracle(value: dict[str, Any]) -> None:
    _require_keys(
        value,
        {
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
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
        raise RuntimeError("Constants metadata schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Constants metadata cases hash drifted.")
    if value["case_sha256"] != case_sha256(value["cases"]):
        raise RuntimeError("Constants metadata per-case hash map drifted.")
    _validate_safe_tree(value)

    definitions = case_definitions()
    cases = value["cases"]
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Constants metadata case order/count drifted.")
    by_id = {item["id"]: item for item in definitions}
    fact_hashes: dict[str, str] = {}
    for case in cases:
        definition = by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Constants metadata case contract drifted: {case['id']}")
        _require_keys(case["python"], {"facts", "facts_sha256", "outcome"}, "python")
        if case["python"]["outcome"] != "observed":
            raise RuntimeError(f"Constants metadata outcome drifted: {case['id']}")
        fact_hash = canonical_sha256(case["python"]["facts"])
        if case["python"]["facts_sha256"] != fact_hash:
            raise RuntimeError(f"Constants metadata inline fact hash drifted: {case['id']}")
        fact_hashes[case["id"]] = fact_hash
        _validate_case_facts(case["id"], case["python"]["facts"])
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Constants metadata fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and value["fact_sha256"] != EXPECTED_FACT_SHA256:
        raise RuntimeError("Constants metadata expected fact hashes drifted.")
    if EXPECTED_CASE_SHA256 and value["case_sha256"] != EXPECTED_CASE_SHA256:
        raise RuntimeError("Constants metadata expected case hashes drifted.")

    target_counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if set(target_counts) != set(TARGET_SYMBOLS) or any(count < 1 for count in target_counts.values()):
        raise RuntimeError("Constants metadata target coverage drifted.")
    if set(RESOLVED_SYMBOLS).intersection(target_counts):
        raise RuntimeError("Resolved constants symbols were retargeted.")
    if Counter(CLASSIFICATIONS.values()) != Counter({"exception": 8}):
        raise RuntimeError("Constants metadata classification counts drifted.")
    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Constants metadata consumer contract drifted.")
    if value["resolved_receipts"] != _indexed(RESOLVED_RECEIPTS):
        raise RuntimeError("Constants metadata resolved receipts drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Constants metadata runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Constants metadata upstream receipt drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("Constants metadata symbol descriptors drifted.")
    if value["target_receipts"] != _indexed(TARGET_RECEIPTS):
        raise RuntimeError("Constants metadata indexed target receipts drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for this oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if not sys.dont_write_bytecode:
        raise SystemExit("Bytecode writes must be disabled for isolated source imports.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
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
    print(f"Wrote constants metadata oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

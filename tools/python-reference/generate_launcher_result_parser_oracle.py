"""Generate the pinned ``idragon/launcher.py`` result-parser oracle.

The corpus binds exactly seven public symbols through three deterministic cases
per symbol.  It directly loads the inventoried launcher source without running
``idragon.__init__`` and records pandas values through a strict JSON-safe
normal form.
"""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import re
import sys
import tempfile
import types
from typing import Any, Callable, Iterator


SCHEMA = "dragons.python-reference.launcher-result-parser.v1"
SOURCE_PATH = "src/idragon/launcher.py"
IMPORT_RELATIVE_PATH = Path("idragon") / "launcher.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f"
)
EXPECTED_SYMBOL_RECEIPTS = {
    "EnergyPlusResult": {
        "body_hash": "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "kind": "class",
        "signature_hash": "sha256:e88e44c74b7fe4452c4b4ab02a77089cc4d00bf85c9ae6e0d66da6f9434f3058",
        "symbol_hash": "sha256:eab88d95447b32529789bf2881a5d4d3e13651c4e10d9bd732a6a647f1a1f597",
    },
    "EnergyPlusResult.__init__": {
        "body_hash": "sha256:07b3bd52cbf73e0aefff8c1ad129513d27a70b4f2eb20367823906d0eb554ded",
        "kind": "function",
        "signature_hash": "sha256:7e5b8274a07d7fb62744d1264fbb0b20e61cd8c98043fadd006e927ad1f5b306",
        "symbol_hash": "sha256:30d49efa5495acff6cb5c9c03c9aacb3bd633048bb4fec63ce2d31b36f85a31a",
    },
    "EnergyPlusResult.parse_audit": {
        "body_hash": "sha256:1623e7f8578b27f3cfb1fb619812d5bd86ff454bd5013ccceae56368e95b13c0",
        "kind": "function",
        "signature_hash": "sha256:1e71b92b3165cc8d006395d5ea0af9a81aff2a071bab1786e6fa831eb183d23f",
        "symbol_hash": "sha256:7315fbc33d50d14f5dfcab78401ef66838a9e2c3760bea72e6b3b3d3aea59fce",
    },
    "EnergyPlusResult.parse_bnd": {
        "body_hash": "sha256:fbb91620c064f38be0b8be747a217029a890610b5e534156afc8f33e01d8b61d",
        "kind": "function",
        "signature_hash": "sha256:46ce611e7c31e66b237299dd1fcfd62fa99a3ace82a364e0bb5f00608b7dbcb1",
        "symbol_hash": "sha256:631c7884e2ca51b8410312a506edf7fbf79928a5c49d13c132e3dc897a325ac9",
    },
    "EnergyPlusResult.parse_err": {
        "body_hash": "sha256:8e8874e9cc6f51ad980d80a7ea7d2f31edfb2fce7cd7b6146322386a68e9d4b3",
        "kind": "function",
        "signature_hash": "sha256:eb51fd10a2d723663f382cb82e19b2ce1c5fad4663d610da9299279287665fa5",
        "symbol_hash": "sha256:f578930710efdaf9f65ec2ec992fc1fcadb22270c26b788a2513125e029e0561",
    },
    "EnergyPlusResult.parse_eso": {
        "body_hash": "sha256:69ee3e6f0fb7d958909b14349d6313a2b89d827e22269768d74cb04ec38a4b37",
        "kind": "function",
        "signature_hash": "sha256:537014c1413f0afaa5e35f17842785640f506e5da03814c6547486d26c06955c",
        "symbol_hash": "sha256:3e849bcd62a1caba6f2d56d3f5353d485f601aa2c9375ffecfa17ab4968d645c",
    },
    "EnergyPlusResult.parse_table": {
        "body_hash": "sha256:6cd0846d65f6edd670890f719cd0c2f793c2fc03a7b07d3f18a7620b38ae4559",
        "kind": "function",
        "signature_hash": "sha256:8d4aab53f7cf5437b4388d7289738711f41221a5fabf818c61bfda9a5a64a8b9",
        "symbol_hash": "sha256:eaf18f211cbe4342c5c5be02cf6ccf1ea10f6604cbf199051bacac03bcabea3a",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_SYMBOL_HASHES = {
    symbol: receipt["symbol_hash"]
    for symbol, receipt in EXPECTED_SYMBOL_RECEIPTS.items()
}
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "EnergyPlusResult": "immutable-structured-energyplus-result",
    "EnergyPlusResult.__init__": "validated-energyplus-result-file-loading",
    "EnergyPlusResult.parse_audit": "ordered-typed-energyplus-audit-parsing",
    "EnergyPlusResult.parse_bnd": "csv-aware-energyplus-boundary-parsing",
    "EnergyPlusResult.parse_err": "structured-energyplus-error-log-parsing",
    "EnergyPlusResult.parse_eso": "explicitly-unsupported-energyplus-eso",
    "EnergyPlusResult.parse_table": "typed-energyplus-tabular-parsing",
}
EXPECTED_ASSERTION_IDS = {
    "EnergyPlusResult": "launcher-result-energyplus-result-eab88d95",
    "EnergyPlusResult.__init__": "launcher-result-init-30d49efa",
    "EnergyPlusResult.parse_audit": "launcher-result-parse-audit-7315fbc3",
    "EnergyPlusResult.parse_bnd": "launcher-result-parse-bnd-631c7884",
    "EnergyPlusResult.parse_err": "launcher-result-parse-err-f5789307",
    "EnergyPlusResult.parse_eso": "launcher-result-parse-eso-3e849bcd",
    "EnergyPlusResult.parse_table": "launcher-result-parse-table-eaf18f21",
}
EXPECTED_CASE_COUNT = 21
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
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

ORACLE_KEYS = {
    "cases", "cases_sha256", "consumer_contract", "runtime", "schema",
    "symbols", "upstream",
}
CASE_KEYS = {"executor", "expected_dotnet", "id", "python", "symbol"}
CASE_DEFINITION_KEYS = {"executor", "expected_dotnet", "id", "symbol"}
EXPECTED_DOTNET_KEYS = {"adaptation", "outcome"}
PYTHON_RETURN_KEYS = {"facts", "outcome"}
CONSUMER_CONTRACT_KEYS = {
    "adaptations", "assertion_ids", "case_count", "case_ids",
    "classifications", "dataframe_encoding", "float_encoding",
    "runtime_names", "target_symbols",
}
RUNTIME_KEYS = {
    "dependencies", "implementation", "python_hash_algorithm",
    "python_hash_seed", "python_hash_width_bits", "python_version",
}
UPSTREAM_KEYS = {"commit", "inventory_sha256", "path", "source_sha256"}
SYMBOL_KEYS = {
    "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash",
}
RETURNED_OBSERVATION_KEYS = {"label", "outcome", "result"}
RAISED_OBSERVATION_KEYS = {
    "error_category", "exception_type", "label", "message", "outcome",
}
FRAME_KEYS = {"columns", "dtypes", "index", "index_name", "rows"}
RAW_ADDRESS_PATTERN = re.compile(
    r"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])"
)
ABSOLUTE_PATH_PATTERN = re.compile(r"(?:^[A-Za-z]:[\\/]|^\\\\)")
BINARY64_PATTERN = re.compile(r"^-?(?:[0-9a-f]+\.[0-9a-f]+p[+-][0-9]+)$")


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_common_core_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_launcher_result_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load launcher oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("Launcher oracle support is not pinned.")
    return module


BASE = _load_support()
strict_json_dumps = BASE.strict_json_dumps
canonical_sha256 = BASE.canonical_sha256
sha256_file = BASE.sha256_file


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    support = BASE.BASE.BASE
    names = (
        "SOURCE_PATH", "EXPECTED_SOURCE_SHA256", "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(support, name) for name in names}
    try:
        support.SOURCE_PATH = SOURCE_PATH
        support.EXPECTED_SOURCE_SHA256 = EXPECTED_SOURCE_SHA256
        support.EXPECTED_SYMBOL_HASHES = EXPECTED_SYMBOL_HASHES
        support.TARGET_SYMBOLS = TARGET_SYMBOLS
        inventory = support.load_exact_inventory(path, upstream_commit)
    finally:
        for name, value in original.items():
            setattr(support, name, value)
    if [item["symbol"] for item in inventory["symbols"]] != list(TARGET_SYMBOLS):
        raise SystemExit("The inventory does not exactly cover seven launcher symbols.")
    for item in inventory["symbols"]:
        expected = {
            **EXPECTED_SYMBOL_RECEIPTS[item["symbol"]],
            "path": SOURCE_PATH,
            "symbol": item["symbol"],
        }
        if item != expected:
            raise SystemExit(f"The inventory receipt for {item['symbol']!r} is not exact.")
    return inventory


def _case(identifier: str, executor: str, symbol: str) -> dict[str, Any]:
    return {
        "executor": executor,
        "expected_dotnet": {
            "adaptation": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS[symbol],
            "outcome": "returned",
        },
        "id": identifier,
        "symbol": symbol,
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = (
        _case("energyplus-result.class-descriptors", "class", "EnergyPlusResult"),
        _case("energyplus-result.class-dynamic-identity", "class", "EnergyPlusResult"),
        _case("energyplus-result.class-static-bindings", "class", "EnergyPlusResult"),
        _case("energyplus-result.init-defaults", "init", "EnergyPlusResult.__init__"),
        _case("energyplus-result.init-dispatch-overwrite", "init", "EnergyPlusResult.__init__"),
        _case("energyplus-result.init-failure-transactionality", "init", "EnergyPlusResult.__init__"),
        _case("energyplus-result.parse-audit-duplicates-unicode", "parse-audit", "EnergyPlusResult.parse_audit"),
        _case("energyplus-result.parse-audit-failure-surface", "parse-audit", "EnergyPlusResult.parse_audit"),
        _case("energyplus-result.parse-audit-recognition-boundaries", "parse-audit", "EnergyPlusResult.parse_audit"),
        _case("energyplus-result.parse-bnd-duplicates-padding", "parse-bnd", "EnergyPlusResult.parse_bnd"),
        _case("energyplus-result.parse-bnd-failure-grammar", "parse-bnd", "EnergyPlusResult.parse_bnd"),
        _case("energyplus-result.parse-bnd-records", "parse-bnd", "EnergyPlusResult.parse_bnd"),
        _case("energyplus-result.parse-err-diagnostics", "parse-err", "EnergyPlusResult.parse_err"),
        _case("energyplus-result.parse-err-failure-surface", "parse-err", "EnergyPlusResult.parse_err"),
        _case("energyplus-result.parse-err-time-empty", "parse-err", "EnergyPlusResult.parse_err"),
        _case("energyplus-result.parse-eso-arity", "parse-eso", "EnergyPlusResult.parse_eso"),
        _case("energyplus-result.parse-eso-opaque", "parse-eso", "EnergyPlusResult.parse_eso"),
        _case("energyplus-result.parse-eso-values", "parse-eso", "EnergyPlusResult.parse_eso"),
        _case("energyplus-result.parse-table-csv-multi-report", "parse-table", "EnergyPlusResult.parse_table"),
        _case("energyplus-result.parse-table-failure-surface", "parse-table", "EnergyPlusResult.parse_table"),
        _case("energyplus-result.parse-table-grammar-duplicates", "parse-table", "EnergyPlusResult.parse_table"),
    )
    result = tuple(sorted(definitions, key=lambda item: item["id"]))
    if len(result) != EXPECTED_CASE_COUNT:
        raise RuntimeError("The launcher oracle must contain exactly 21 cases.")
    return result


def _scalar(value: Any) -> dict[str, Any]:
    if value is None:
        return {"kind": "none"}
    if type(value) is bool:
        return {"kind": "bool", "value": value}
    if hasattr(value, "item") and not isinstance(value, (str, bytes)):
        try:
            value = value.item()
        except ValueError:
            pass
    if type(value) is int:
        return {"decimal": str(value), "kind": "int"}
    if type(value) is float:
        if math.isnan(value):
            return {"kind": "nan"}
        if math.isinf(value):
            return {"kind": "positive-infinity" if value > 0 else "negative-infinity"}
        return {"binary64": value.hex().removeprefix("0x"), "kind": "float"}
    if type(value) is str:
        return {"kind": "string", "value": value}
    raise RuntimeError(f"Unsupported scalar type: {type(value).__name__}")


def _frame(value: Any) -> dict[str, Any]:
    return {
        "columns": [str(item) for item in value.columns.tolist()],
        "dtypes": [str(item) for item in value.dtypes.tolist()],
        "index": [_scalar(item) for item in value.index.tolist()],
        "index_name": _scalar(value.index.name),
        "rows": [
            [_scalar(item) for item in row]
            for row in value.itertuples(index=False, name=None)
        ],
    }


def _audit(value: dict[Any, Any]) -> list[dict[str, Any]]:
    return [{"key": str(key), "value": _scalar(item)} for key, item in value.items()]


def _frame_map(value: dict[Any, Any]) -> list[dict[str, Any]]:
    return [{"frame": _frame(item), "key": str(key)} for key, item in value.items()]


def _error_result(value: tuple[float, Any]) -> dict[str, Any]:
    elapsed, warnings = value
    return {"elapsed_seconds": _scalar(elapsed), "warnings": _frame(warnings)}


def _exception_category(error: Exception) -> str:
    names = {
        "AttributeError": "missing-member",
        "FileNotFoundError": "missing-file",
        "ParserError": "invalid-csv",
        "TypeError": "invalid-type",
        "UnboundLocalError": "unbound-local",
        "ValueError": "invalid-value",
    }
    return names.get(type(error).__name__, "unexpected-error")


def _message(error: Exception, temp_root: Path | None) -> str:
    message = str(error).replace("\\", "/")
    if temp_root is not None:
        variants = {str(temp_root), str(temp_root.resolve())}
        for variant in sorted(variants, key=len, reverse=True):
            message = message.replace(variant.replace("\\", "/"), "<TEMP>")
    message = re.sub(r"'(?:Windows|Posix)Path'", "'pathlib.Path'", message)
    return message


def _observe(
    label: str,
    action: Callable[[], Any],
    normalizer: Callable[[Any], Any] = _scalar,
    temp_root: Path | None = None,
) -> dict[str, Any]:
    try:
        return {"label": label, "outcome": "returned", "result": normalizer(action())}
    except Exception as error:  # noqa: BLE001 - exception surface is the fixture.
        return {
            "error_category": _exception_category(error),
            "exception_type": type(error).__name__,
            "label": label,
            "message": _message(error, temp_root),
            "outcome": "raised",
        }


def _write_text(root: Path, name: str, text: str) -> str:
    path = root / name
    path.write_text(text, encoding="utf-8", newline="")
    return str(path)


def _report(
    name: str,
    body: str,
    *,
    prefix: str = "\n",
    ending: str = "\n\n",
    scope: str = "Meter",
) -> str:
    return (
        f"{prefix}REPORT:,{name}\nFOR:,{scope}\nCustom Monthly Report\n\n"
        f"{body}{ending}"
    )


def _execute_class(identifier: str, result_type: type) -> dict[str, Any]:
    methods = ("parse_audit", "parse_err", "parse_bnd", "parse_table", "parse_eso")
    if identifier.endswith("class-descriptors"):
        return {
            "base_names": [item.__name__ for item in result_type.__bases__],
            "direct_dictionary_keys": list(result_type.__dict__.keys()),
            "method_signatures": {
                name: str(inspect.signature(getattr(result_type, name))) for name in methods
            },
            "module": result_type.__module__,
            "name": result_type.__name__,
            "signature": str(inspect.signature(result_type)),
        }
    if identifier.endswith("class-dynamic-identity"):
        first = result_type.__new__(result_type)
        second = result_type.__new__(result_type)
        first.extra = 7
        class Derived(result_type):
            pass
        return {
            "arbitrary_attribute": _scalar(first.extra),
            "hashable": isinstance(hash(first), int),
            "identity": {
                "separate_instances_equal": first == second,
                "self_equal": first == first,
            },
            "subclass": {
                "instance_of_base": isinstance(Derived(), result_type),
                "mro": [item.__name__ for item in Derived.__mro__],
            },
        }
    instance = result_type()
    return {
        "descriptor_types": {
            name: type(result_type.__dict__[name]).__name__ for name in methods
        },
        "same_function_identity": {
            name: getattr(result_type, name) is getattr(instance, name) for name in methods
        },
    }


def _execute_init(identifier: str, result_type: type) -> dict[str, Any]:
    if identifier.endswith("init-defaults"):
        result = result_type()
        return {
            "attribute_order": list(result.__dict__.keys()),
            "has_time": hasattr(result, "time"),
            "values": {name: _scalar(getattr(result, name)) for name in result.__dict__},
        }
    with tempfile.TemporaryDirectory(prefix="launcher-result-oracle-") as directory:
        root = Path(directory)
        first_audit = root / "first.audit"
        first_audit.write_bytes(b"\xffAlpha= 1\nDup= 2\nDup= 3")
        second_audit = _write_text(root, "second.audit", "Final= 9")
        error_path = _write_text(
            root,
            "eplusout.err",
            "** Warning ** first\n** Severe ** second\nElapsed Time=1hr 2min 3.25sec",
        )
        boundary_path = _write_text(root, "eplusout.bnd", "<Rec>,<A>,<B>\nRec,x")
        table_path = _write_text(
            root,
            "eplustbl.csv",
            _report("R", ",K,V\n,A,4"),
        )
        eso_path = _write_text(root, "eplusout.eso", "ignored")
        upper_path = _write_text(root, "case.ERR", "ignored")
        unknown_path = _write_text(root, "notes.txt", "ignored")
        if identifier.endswith("init-dispatch-overwrite"):
            invalid_utf8 = result_type(str(first_audit))
            ignored = result_type(upper_path, unknown_path)
            final = result_type(
                str(first_audit), error_path, boundary_path, table_path, eso_path,
                upper_path, unknown_path, second_audit,
            )
            return {
                "final": {
                    "attribute_order": list(final.__dict__.keys()),
                    "audit": _audit(final.audit),
                    "boundary": _frame_map(final.bnd),
                    "error": _frame(final.err),
                    "eso": _scalar(final.eso),
                    "table": _frame_map(final.tbl),
                    "time": _scalar(final.time),
                },
                "ignored_suffixes": {
                    "attribute_order": list(ignored.__dict__.keys()),
                    "all_none": all(value is None for value in ignored.__dict__.values()),
                    "has_time": hasattr(ignored, "time"),
                },
                "invalid_utf8_audit": _audit(invalid_utf8.audit),
            }
        missing = root / "missing.err"
        path_audit = root / "path.audit"
        path_audit.write_text("A= 1", encoding="utf-8")
        bad_error = _write_text(root, "bad.err", "no elapsed marker")
        bad_html = _write_text(root, "eplustbl.html", "anything")
        bad_xml = _write_text(root, "eplustbl.xml", "anything")
        observations = [
            _observe("missing-file", lambda: result_type(str(missing)), temp_root=root),
            _observe("path-object", lambda: result_type(path_audit), temp_root=root),
            _observe("malformed-error", lambda: result_type(bad_error), temp_root=root),
            _observe("html-table", lambda: result_type(bad_html), temp_root=root),
            _observe("xml-table", lambda: result_type(bad_xml), temp_root=root),
        ]
        partial = result_type.__new__(result_type)
        partial_error = _observe(
            "later-missing-file",
            lambda: result_type.__init__(partial, str(first_audit), str(missing)),
            temp_root=root,
        )
        return {
            "observations": observations,
            "partial_state": {
                "audit": _audit(partial.audit),
                "attribute_order": list(partial.__dict__.keys()),
                "failure": partial_error,
            },
        }


def _execute_audit(identifier: str, result_type: type) -> dict[str, Any]:
    if identifier.endswith("duplicates-unicode"):
        text = (
            "A= 1\nB= 2\nA= 3\n"
            "\ud55c\uae00_\u0661= \u0664\u0662\n"
            "Huge= 99999999999999999999999999999999999999"
        )
        return {"entries": _audit(result_type.parse_audit(text))}
    if identifier.endswith("failure-surface"):
        values = ("", "noise", None, b"A= 1", 7)
        labels = ("empty", "noise", "none", "bytes", "integer")
        return {
            "observations": [
                _observe(label, lambda value=value: result_type.parse_audit(value), _audit)
                for label, value in zip(labels, values)
            ]
        }
    text = (
        "prefix Alpha= 12\nNoSpace=3\nSpaced = 4\nNegative= -5\n"
        "Plus= +6\nDecimal= 7.5\nTab=\t8\nTrailing= 09units\nx-y= 10"
    )
    return {"entries": _audit(result_type.parse_audit(text))}


def _execute_bnd(identifier: str, result_type: type) -> dict[str, Any]:
    if identifier.endswith("duplicates-padding"):
        text = "<Rec>,<Old>\n<Rec>,<A>,<B>,<C>\nRec,one,,three"
        return {"tables": _frame_map(result_type.parse_bnd(text))}
    if identifier.endswith("failure-grammar"):
        observations = (
            ("overlong-row", "<Rec>,<A>,<B>\nRec,1,2,3"),
            ("partial-invalid-column", "<Rec>,<A>,<B-C>\nRec,1,2"),
            ("invalid-header", "<Bad-Key>,<A-B>\nBad-Key,x"),
            ("none", None),
            ("bytes", b"<Rec>,<A>"),
        )
        return {
            "observations": [
                _observe(label, lambda text=text: result_type.parse_bnd(text), _frame_map)
                for label, text in observations
            ]
        }
    text = (
        "Zone Information,Ignored,0\n"
        "<Zone Information>,<Zone Name>,<Zone #>,<Path [m2/s]>\n"
        "<Surface Data>,<Name>,<Type>\n"
        "Zone Information,Zone A,1\nUnknown,a,b\n"
        "Zone Information,Zone B,2,/tmp"
    )
    first_line = "Rec,first\n<Rec>,<A>\nRec,second"
    return {
        "first_line": _frame_map(result_type.parse_bnd(first_line)),
        "tables": _frame_map(result_type.parse_bnd(text)),
    }


def _execute_err(identifier: str, result_type: type) -> dict[str, Any]:
    if identifier.endswith("diagnostics"):
        text = (
            "preamble\n** Warning **  first title  \n** Severe ** second title\n"
            "** Fatal ** ignored fatal\n** warning ** ignored lower\n"
            "** ~~~ ** continuation ignored\nElapsed Time=1hr  2min\t3.25sec\n"
            "Elapsed Time=9hr 9min 9sec"
        )
        return _error_result(result_type.parse_err(text))
    if identifier.endswith("failure-surface"):
        observations = (
            ("missing", ""),
            ("spaced-equals", "Elapsed Time =0hr 0min 1sec"),
            ("invalid-seconds", "Elapsed Time=0hr 0min 1..2sec"),
            ("none", None),
            ("bytes", b"Elapsed Time=0hr 0min 1sec"),
        )
        return {
            "observations": [
                _observe(label, lambda text=text: result_type.parse_err(text), _error_result)
                for label, text in observations
            ]
        }
    return _error_result(result_type.parse_err("Elapsed Time=0hr \n 0min .5sec"))


def _execute_eso(identifier: str, result_type: type) -> dict[str, Any]:
    if identifier.endswith("arity"):
        return {
            "observations": [
                _observe("zero", lambda: result_type.parse_eso()),
                _observe("two", lambda: result_type.parse_eso("a", "b")),
                _observe("keyword", lambda: result_type.parse_eso(text="x")),
                _observe("wrong-keyword", lambda: result_type.parse_eso(value="x")),
            ]
        }
    if identifier.endswith("opaque"):
        class Opaque:
            def __getattribute__(self, name: str) -> Any:
                raise AssertionError(f"input was inspected: {name}")

            def __repr__(self) -> str:
                raise AssertionError("input was represented")

        return {"observation": _observe("opaque", lambda: result_type.parse_eso(Opaque()))}
    values = ("", "text", None, b"x", 7, [1, 2])
    labels = ("empty", "text", "none", "bytes", "integer", "list")
    return {
        "observations": [
            _observe(label, lambda value=value: result_type.parse_eso(value))
            for label, value in zip(labels, values)
        ]
    }


def _execute_table(identifier: str, result_type: type) -> dict[str, Any]:
    if identifier.endswith("csv-multi-report"):
        text = (
            _report(
                "MonthlyOne",
                ',Month,Electricity [kWh],Label\n,Jan,1.5,"a,b"\n,,,\n,Feb,,plain',
            )
            + "separator"
            + _report("Second_2", ",Key,Value\n,A,2\n,B,3")
        )
        return {"tables": _frame_map(result_type.parse_table(text, "csv"))}
    if identifier.endswith("failure-surface"):
        observations = [
            _observe(
                f"extension-{str(extension).lower()}",
                lambda extension=extension: result_type.parse_table("", extension),
                _frame_map,
            )
            for extension in ("html", "xml", "CSV", None, 1)
        ]
        malformed = _report("Bad", ',A,B\n,row,"unterminated')
        observations.extend(
            (
                _observe("none-text", lambda: result_type.parse_table(None, "csv"), _frame_map),
                _observe("bytes-text", lambda: result_type.parse_table(b"", "csv"), _frame_map),
                _observe("malformed-csv", lambda: result_type.parse_table(malformed, "csv"), _frame_map),
            )
        )
        return {"observations": observations}
    separated = _report("Dup", ",Key,Value\n,A,1") + "separator" + _report(
        "Dup", ",Key,Value\n,B,2"
    )
    adjacent = _report("Dup", ",Key,Value\n,A,1") + _report(
        "Dup", ",Key,Value\n,B,2"
    )
    forms = (
        ("leading", _report("Lead", ",K,V\n,A,1", prefix="")),
        ("spaced-name", _report("Monthly Report", ",K,V\n,A,1")),
        ("wrong-scope", _report("X", ",K,V\n,A,1", scope="Building")),
        ("crlf", _report("CRLF", ",K,V\n,A,1").replace("\n", "\r\n")),
        ("unterminated", _report("NoEnd", ",K,V\n,A,1", ending="")),
        ("duplicates-separated", separated),
        ("duplicates-adjacent", adjacent),
    )
    return {
        "observations": [
            _observe(label, lambda text=text: result_type.parse_table(text, "csv"), _frame_map)
            for label, text in forms
        ]
    }


EXECUTORS: dict[str, Callable[[str, type], dict[str, Any]]] = {
    "class": _execute_class,
    "init": _execute_init,
    "parse-audit": _execute_audit,
    "parse-bnd": _execute_bnd,
    "parse-err": _execute_err,
    "parse-eso": _execute_eso,
    "parse-table": _execute_table,
}


def _find_pinned_source() -> Path:
    candidates: dict[str, Path] = {}
    for entry in sys.path:
        if not entry:
            continue
        candidate = Path(entry) / IMPORT_RELATIVE_PATH
        try:
            if not candidate.is_file():
                continue
            resolved = candidate.resolve(strict=True)
        except OSError:
            continue
        if sha256_file(resolved) == EXPECTED_SOURCE_SHA256:
            candidates[str(resolved).casefold()] = resolved
    if len(candidates) != 1:
        raise SystemExit(
            "Expected exactly one byte-pinned idragon/launcher.py on the bootstrap "
            f"path, found {len(candidates)}."
        )
    return next(iter(candidates.values()))


def _load_file(name: str, path: Path) -> Any:
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise SystemExit(f"Cannot directly import pinned source: {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    if Path(module.__file__).resolve() != path.resolve():
        raise SystemExit(f"Imported source path drifted: {path}")
    return module


@contextmanager
def _pinned_launcher(source: Path) -> Iterator[Any]:
    if sha256_file(source) != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The selected launcher.py is not the exact pinned source.")
    parent = source.parent
    paths = {
        "idragon.constants": parent / "constants.py",
        "idragon.common": parent / "common.py",
        "idragon.launcher": source,
    }
    names = ("idragon", *paths)
    previous = {name: sys.modules.get(name) for name in names}
    package = types.ModuleType("idragon")
    package.__package__ = "idragon"
    package.__path__ = [str(parent)]
    try:
        sys.modules["idragon"] = package
        _load_file("idragon.constants", paths["idragon.constants"])
        _load_file("idragon.common", paths["idragon.common"])
        launcher = _load_file("idragon.launcher", source)
        if launcher.EnergyPlusResult.__module__ != "idragon.launcher":
            raise SystemExit("Pinned launcher module identity drifted.")
        yield launcher
    finally:
        for name in reversed(names):
            prior = previous[name]
            if prior is None:
                sys.modules.pop(name, None)
            else:
                sys.modules[name] = prior


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _dependencies() -> dict[str, str]:
    return {name: importlib.metadata.version(name) for name in EXPECTED_DEPENDENCIES}


def build_oracle(
    inventory: dict[str, Any],
    commit: str,
    source: Path | None = None,
) -> dict[str, Any]:
    imported_source = source.resolve() if source is not None else _find_pinned_source()
    imported_sha256 = sha256_file(imported_source)
    if imported_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported launcher.py is not the inventoried source.")
    definitions = case_definitions()
    with _pinned_launcher(imported_source) as module:
        result_type = module.EnergyPlusResult
        cases = []
        for definition in definitions:
            case = {
                "executor": definition["executor"],
                "expected_dotnet": definition["expected_dotnet"],
                "id": definition["id"],
                "python": {
                    "facts": EXECUTORS[definition["executor"]](
                        definition["id"], result_type
                    ),
                    "outcome": "returned",
                },
                "symbol": definition["symbol"],
            }
            cases.append(case)
    result = {
        "cases": cases,
        "cases_sha256": cases_sha256(cases),
        "consumer_contract": {
            "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
            "assertion_ids": EXPECTED_ASSERTION_IDS,
            "case_count": EXPECTED_CASE_COUNT,
            "case_ids": [item["id"] for item in definitions],
            "classifications": {symbol: "exception" for symbol in TARGET_SYMBOLS},
            "dataframe_encoding": "ordered-columns-index-dtypes-and-tagged-cells",
            "float_encoding": "python-binary64-hex-without-0x-prefix",
            "runtime_names": "pinned-python-only-no-native-type-name-claims",
            "target_symbols": list(TARGET_SYMBOLS),
        },
        "runtime": {
            "dependencies": _dependencies(),
            "implementation": sys.implementation.name,
            "python_hash_algorithm": sys.hash_info.algorithm,
            "python_hash_seed": 0,
            "python_hash_width_bits": sys.hash_info.width,
            "python_version": ".".join(map(str, sys.version_info[:3])),
        },
        "schema": SCHEMA,
        "symbols": inventory["symbols"],
        "upstream": {
            "commit": commit,
            "inventory_sha256": inventory["content_sha256"],
            "path": SOURCE_PATH,
            "source_sha256": imported_sha256,
        },
    }
    validate_oracle(result)
    return result


def _require_keys(value: Any, expected: set[str], location: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(f"{location} key set drifted: {actual!r}")


def _validate_scalar(value: Any, location: str) -> None:
    if not isinstance(value, dict) or "kind" not in value:
        raise RuntimeError(f"{location} is not a tagged scalar.")
    kind = value["kind"]
    expected = {
        "none": {"kind"}, "nan": {"kind"},
        "positive-infinity": {"kind"}, "negative-infinity": {"kind"},
        "bool": {"kind", "value"}, "int": {"decimal", "kind"},
        "float": {"binary64", "kind"}, "string": {"kind", "value"},
    }.get(kind)
    if expected is None or set(value) != expected:
        raise RuntimeError(f"{location} tagged scalar shape drifted.")
    if kind == "float" and not BINARY64_PATTERN.fullmatch(value["binary64"]):
        raise RuntimeError(f"{location} binary64 encoding drifted.")
    if kind == "int" and not re.fullmatch(r"-?(?:0|[1-9][0-9]*)", value["decimal"]):
        raise RuntimeError(f"{location} integer encoding drifted.")


def _validate_frame(value: Any, location: str) -> None:
    _require_keys(value, FRAME_KEYS, location)
    if not all(isinstance(item, str) for item in value["columns"]):
        raise RuntimeError(f"{location} columns drifted.")
    if len(value["columns"]) != len(value["dtypes"]):
        raise RuntimeError(f"{location} dtype count drifted.")
    _validate_scalar(value["index_name"], f"{location}.index_name")
    for index, item in enumerate(value["index"]):
        _validate_scalar(item, f"{location}.index[{index}]")
    if len(value["index"]) != len(value["rows"]):
        raise RuntimeError(f"{location} row/index count drifted.")
    for row_index, row in enumerate(value["rows"]):
        if len(row) != len(value["columns"]):
            raise RuntimeError(f"{location}.rows[{row_index}] width drifted.")
        for column_index, item in enumerate(row):
            _validate_scalar(item, f"{location}.rows[{row_index}][{column_index}]")


def _validate_tree(value: Any, location: str = "root") -> None:
    if type(value) is float:
        raise RuntimeError(f"Raw float entered {location}.")
    if isinstance(value, Path):
        raise RuntimeError(f"Raw path entered {location}.")
    if isinstance(value, str):
        if RAW_ADDRESS_PATTERN.search(value):
            raise RuntimeError(f"A raw runtime address entered {location}.")
        if ABSOLUTE_PATH_PATTERN.search(value):
            raise RuntimeError(f"An absolute path entered {location}.")
        return
    if value is None or type(value) in (bool, int):
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_tree(item, f"{location}[{index}]")
        return
    if isinstance(value, dict):
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"A non-string key entered {location}.")
            _validate_tree(item, f"{location}.{key}")
        return
    raise RuntimeError(f"Raw object {type(value).__name__} entered {location}.")


def _case_by_id(value: dict[str, Any], identifier: str) -> dict[str, Any]:
    matches = [item for item in value["cases"] if item["id"] == identifier]
    if len(matches) != 1:
        raise RuntimeError(f"Expected exactly one case {identifier!r}.")
    return matches[0]


def _validate_observations(items: Any, location: str) -> None:
    if not isinstance(items, list):
        raise RuntimeError(f"{location} observations are not a list.")
    for index, item in enumerate(items):
        expected = (
            RETURNED_OBSERVATION_KEYS
            if isinstance(item, dict) and item.get("outcome") == "returned"
            else RAISED_OBSERVATION_KEYS
        )
        _require_keys(item, expected, f"{location}[{index}]")


def _validate_semantics(value: dict[str, Any]) -> None:
    defaults = _case_by_id(value, "energyplus-result.init-defaults")["python"]["facts"]
    if defaults != {
        "attribute_order": ["audit", "err", "bnd", "tbl", "eso"],
        "has_time": False,
        "values": {name: {"kind": "none"} for name in ("audit", "err", "bnd", "tbl", "eso")},
    }:
        raise RuntimeError("Constructor default semantics drifted.")
    audit = _case_by_id(value, "energyplus-result.parse-audit-recognition-boundaries")["python"]["facts"]["entries"]
    if [(item["key"], item["value"].get("decimal")) for item in audit] != [
        ("Alpha", "12"), ("Decimal", "7"), ("Tab", "8"),
        ("Trailing", "9"), ("y", "10"),
    ]:
        raise RuntimeError("Audit recognition semantics drifted.")
    duplicate = _case_by_id(value, "energyplus-result.parse-audit-duplicates-unicode")["python"]["facts"]["entries"]
    if [item["key"] for item in duplicate] != ["A", "B", "\ud55c\uae00_\u0661", "Huge"]:
        raise RuntimeError("Audit duplicate/Unicode semantics drifted.")
    padding = _case_by_id(value, "energyplus-result.parse-bnd-duplicates-padding")["python"]["facts"]["tables"][0]["frame"]
    if padding["columns"] != ["A", "B", "C"] or [item["kind"] for item in padding["rows"][0]] != ["string", "none", "none"]:
        raise RuntimeError("Boundary padding semantics drifted.")
    error = _case_by_id(value, "energyplus-result.parse-err-diagnostics")["python"]["facts"]
    if error["elapsed_seconds"] != {"binary64": "1.d168000000000p+11", "kind": "float"}:
        raise RuntimeError("Error elapsed-time semantics drifted.")
    if [row[0]["value"] for row in error["warnings"]["rows"]] != ["Warning", "Severe"]:
        raise RuntimeError("Error diagnostic filtering drifted.")
    eso = _case_by_id(value, "energyplus-result.parse-eso-values")["python"]["facts"]["observations"]
    if not eso or any(item.get("result") != {"kind": "none"} for item in eso):
        raise RuntimeError("ESO no-op semantics drifted.")
    table = _case_by_id(value, "energyplus-result.parse-table-csv-multi-report")["python"]["facts"]["tables"]
    if [item["key"] for item in table] != ["MonthlyOne", "Second_2"]:
        raise RuntimeError("Tabular report ordering drifted.")
    for case in value["cases"]:
        facts = case["python"]["facts"]
        if "observations" in facts:
            _validate_observations(facts["observations"], case["id"])


def validate_oracle(value: dict[str, Any]) -> None:
    _require_keys(value, ORACLE_KEYS, "root")
    if value["schema"] != SCHEMA:
        raise RuntimeError("Oracle schema drifted.")
    definitions = case_definitions()
    identifiers = [item["id"] for item in definitions]
    if len(value["cases"]) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Oracle case count drifted.")
    if [item["id"] for item in value["cases"]] != identifiers:
        raise RuntimeError("Oracle case order drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Oracle cases hash drifted.")
    by_id = {item["id"]: item for item in definitions}
    for case in value["cases"]:
        _require_keys(case, CASE_KEYS, f"case {case.get('id')!r}")
        definition = by_id[case["id"]]
        if any(case[key] != definition[key] for key in ("executor", "expected_dotnet", "symbol")):
            raise RuntimeError(f"Case contract drifted: {case['id']}")
        _require_keys(case["expected_dotnet"], EXPECTED_DOTNET_KEYS, "native expectation")
        _require_keys(case["python"], PYTHON_RETURN_KEYS, f"case {case['id']}.python")
        if case["python"]["outcome"] != "returned" or not isinstance(case["python"]["facts"], dict):
            raise RuntimeError(f"Case Python outcome drifted: {case['id']}")
    contract = value["consumer_contract"]
    _require_keys(contract, CONSUMER_CONTRACT_KEYS, "consumer contract")
    expected_contract = {
        "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        "assertion_ids": EXPECTED_ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": identifiers,
        "classifications": {symbol: "exception" for symbol in TARGET_SYMBOLS},
        "dataframe_encoding": "ordered-columns-index-dtypes-and-tagged-cells",
        "float_encoding": "python-binary64-hex-without-0x-prefix",
        "runtime_names": "pinned-python-only-no-native-type-name-claims",
        "target_symbols": list(TARGET_SYMBOLS),
    }
    if contract != expected_contract:
        raise RuntimeError("The consumer contract drifted.")
    _require_keys(value["runtime"], RUNTIME_KEYS, "runtime")
    expected_runtime = {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }
    if value["runtime"] != expected_runtime:
        raise RuntimeError("The pinned runtime drifted.")
    _require_keys(value["upstream"], UPSTREAM_KEYS, "upstream")
    if value["upstream"] != {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    }:
        raise RuntimeError("The pinned upstream source drifted.")
    if value["symbols"] != [
        {**EXPECTED_SYMBOL_RECEIPTS[symbol], "path": SOURCE_PATH, "symbol": symbol}
        for symbol in TARGET_SYMBOLS
    ]:
        raise RuntimeError("Symbol receipts drifted.")
    for item in value["symbols"]:
        _require_keys(item, SYMBOL_KEYS, "symbol receipt")
    _validate_semantics(value)
    _validate_tree(value)
    serialized = strict_json_dumps(value)
    if RAW_ADDRESS_PATTERN.search(serialized):
        raise RuntimeError("A raw runtime address entered the launcher oracle.")


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the launcher result oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    if _dependencies() != EXPECTED_DEPENDENCIES:
        raise SystemExit("The exact pinned Python dependency set is required.")
    commit = args.upstream_commit.lower()
    inventory = load_exact_inventory(args.inventory, commit)
    result = build_oracle(inventory, commit)
    serialized = strict_json_dumps(result, indent=2) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(serialized, encoding="utf-8", newline="\n")
    print(f"Wrote launcher result-parser oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

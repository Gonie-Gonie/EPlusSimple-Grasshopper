"""Generate the pinned ``idragon/launcher.py`` runtime oracle.

The corpus binds the four public runtime symbols through exactly three closed,
deterministic cases per symbol.  The pinned module is loaded directly without
running ``idragon.__init__``.  EnergyPlus and child processes are never
executed; filesystem behavior is constrained to a unique temporary descendant
and only logical path tokens enter the fixture.
"""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import importlib.util
import inspect
import os
from pathlib import Path
import re
import sys
import tempfile
from typing import Any, Callable, Iterator


SCHEMA = "goniegonie.python-reference.launcher-runtime.v1"
SOURCE_PATH = "src/idragon/launcher.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f"
)
EXPECTED_SYMBOL_RECEIPTS = {
    "ExecutableEnergyPlusNotFoundError": {
        "body_hash": "sha256:921a63a3a05234e5b1c61efbee031114924c6587cc8d60b93d4932290c0b549a",
        "kind": "class",
        "signature_hash": "sha256:af4269c77686b13c3db93174c1cba5a3679b876d7dd4996633b5daeab8fff8f3",
        "symbol_hash": "sha256:76d795db5a0292d2af780a00ee53760a1ab5bd07d50cdf144cf6863c3b08b3d3",
    },
    "find_executable_dir": {
        "body_hash": "sha256:99ab4b26f1043306ed119f8df86069765fa4cde5dd32792ce335ea1800820c2d",
        "kind": "function",
        "signature_hash": "sha256:ebb7af2fdb78b6207f5681aa3c0ccab67f8ba7bd843663597beed49ccf11b61f",
        "symbol_hash": "sha256:6de563f4cfe228449e3c29866c9e432c7cf0f9ffc49dad4e7558d9b0addebf1b",
    },
    "run": {
        "body_hash": "sha256:0fb4f14dabde914d8f39235d9df925f011fc66d7fc88131230fc5b213bff106a",
        "kind": "function",
        "signature_hash": "sha256:4e1ba99373cc367b28141f5395751a8eac81db60ee71bb4afe691981d4bd2bf8",
        "symbol_hash": "sha256:84c6ff241eb023074ab18999d177182fcc33e90c128d479f50303041945b4281",
    },
    "run_single": {
        "body_hash": "sha256:e5fb9f5b5a84a697283db6c5bb88dce0f1b696c7864a2934279ff93a1b3ba659",
        "kind": "function",
        "signature_hash": "sha256:77a80eeb659cc9b40e69a8db74a9337246ca40bb3ea900e9b466accc92bb9c0a",
        "symbol_hash": "sha256:eda7f7577da0c1ac73498136fcfa6955ffb6605bad1dc0b9a1ac80609b094884",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_SYMBOL_HASHES = {
    symbol: receipt["symbol_hash"]
    for symbol, receipt in EXPECTED_SYMBOL_RECEIPTS.items()
}
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "ExecutableEnergyPlusNotFoundError": "structured-energyplus-runtime-not-found-failure",
    "find_executable_dir": "hash-verified-energyplus-runtime-resolution",
    "run": "bounded-deterministic-energyplus-batch-execution",
    "run_single": "isolated-cancellable-energyplus-single-run",
}
EXPECTED_ASSERTION_IDS = {
    "ExecutableEnergyPlusNotFoundError": "launcher-runtime-executable-not-found-76d795db",
    "find_executable_dir": "launcher-runtime-find-executable-dir-6de563f4",
    "run": "launcher-runtime-run-84c6ff24",
    "run_single": "launcher-runtime-run-single-eda7f757",
}
EXPECTED_CASE_COUNT = 12
EXPECTED_CASE_IDS = (
    "launcher-runtime.executable-error-class",
    "launcher-runtime.executable-error-instance",
    "launcher-runtime.executable-error-raise",
    "launcher-runtime.find-executable-failure",
    "launcher-runtime.find-executable-package-precedence",
    "launcher-runtime.find-executable-system-fallback",
    "launcher-runtime.run-broadcast",
    "launcher-runtime.run-cardinality",
    "launcher-runtime.run-scalar",
    "launcher-runtime.run-single-explicit-retain",
    "launcher-runtime.run-single-inferred-delete",
    "launcher-runtime.run-single-transactionality",
)
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

RAW_ADDRESS_PATTERN = re.compile(
    r"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])"
)
ABSOLUTE_PATH_PATTERN = re.compile(
    r"(?i)(?:[a-z]:[\\/]|\\\\[^\\]|(?<![A-Za-z0-9_.<>-])/(?:home|mnt|private|root|tmp|Users|var)(?:/|$))"
)
TEMP_LEAK_PATTERN = re.compile(
    r"(?i)(?:goniegonie-launcher-runtime-oracle-|AppData[\\/]+Local[\\/]+Temp)"
)
GUID_PATTERN = re.compile(
    r"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-"
    r"[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])"
)
TIMESTAMP_PATTERN = re.compile(
    r"(?<!\d)\d{4}-\d{2}-\d{2}[T ][0-2]\d:[0-5]\d:[0-5]\d"
)


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_launcher_result_parser_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_goniegonie_launcher_runtime_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load launcher runtime support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or module.EXPECTED_SOURCE_SHA256 != EXPECTED_SOURCE_SHA256
    ):
        raise RuntimeError("Launcher runtime support is not pinned.")
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
    support = BASE.BASE.BASE.BASE
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
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
        raise SystemExit("The inventory does not exactly cover four launcher symbols.")
    for item in inventory["symbols"]:
        expected = {
            **EXPECTED_SYMBOL_RECEIPTS[item["symbol"]],
            "path": SOURCE_PATH,
            "symbol": item["symbol"],
        }
        if item != expected:
            raise SystemExit(
                f"The inventory receipt for {item['symbol']!r} is not exact."
            )
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
    symbols = (
        "ExecutableEnergyPlusNotFoundError",
        "ExecutableEnergyPlusNotFoundError",
        "ExecutableEnergyPlusNotFoundError",
        "find_executable_dir",
        "find_executable_dir",
        "find_executable_dir",
        "run",
        "run",
        "run",
        "run_single",
        "run_single",
        "run_single",
    )
    executors = (
        "executable-error",
        "executable-error",
        "executable-error",
        "find-executable",
        "find-executable",
        "find-executable",
        "run",
        "run",
        "run",
        "run-single",
        "run-single",
        "run-single",
    )
    definitions = tuple(
        _case(identifier, executor, symbol)
        for identifier, executor, symbol in zip(
            EXPECTED_CASE_IDS, executors, symbols, strict=True
        )
    )
    if tuple(item["id"] for item in definitions) != tuple(sorted(EXPECTED_CASE_IDS)):
        raise RuntimeError("Launcher runtime case IDs are not sorted.")
    if any(
        sum(item["symbol"] == symbol for item in definitions) != 3
        for symbol in TARGET_SYMBOLS
    ):
        raise RuntimeError("Launcher runtime cases are not exactly three per symbol.")
    return definitions


def _error_category(error: Exception) -> str:
    if isinstance(error, FileNotFoundError):
        return "file-not-found"
    if isinstance(error, IndexError):
        return "index"
    if isinstance(error, RuntimeError):
        return "runtime"
    if isinstance(error, TypeError):
        return "type"
    if isinstance(error, ValueError):
        return "value"
    return "other"


def _safe_message(error: Exception, temp_root: Path | None = None) -> str:
    value = str(error)
    if temp_root is not None:
        variants = {
            str(temp_root),
            str(temp_root).replace("\\", "\\\\"),
            str(temp_root).replace("\\", "/"),
        }
        for variant in sorted(variants, key=len, reverse=True):
            value = value.replace(variant, "<controlled-temp>")
    return value.replace("\\", "/")


def _observe(
    label: str,
    action: Callable[[], Any],
    *,
    normalize: Callable[[Any], Any] = lambda value: value,
    temp_root: Path | None = None,
) -> dict[str, Any]:
    try:
        return {
            "label": label,
            "outcome": "returned",
            "result": normalize(action()),
        }
    except Exception as error:  # noqa: BLE001 - the failure surface is evidence.
        return {
            "error_category": _error_category(error),
            "exception_type": type(error).__name__,
            "label": label,
            "message": _safe_message(error, temp_root),
            "outcome": "raised",
        }


@contextmanager
def _patched(target: Any, name: str, value: Any) -> Iterator[None]:
    original = getattr(target, name)
    setattr(target, name, value)
    try:
        yield
    finally:
        setattr(target, name, original)


def _execute_executable_error(identifier: str, module: Any) -> dict[str, Any]:
    error_type = module.ExecutableEnergyPlusNotFoundError
    if identifier.endswith("executable-error-class"):
        return {
            "base_names": [item.__name__ for item in error_type.__bases__],
            "direct_dictionary_keys": sorted(error_type.__dict__),
            "inspect_signature": _observe(
                "class-signature", lambda: str(inspect.signature(error_type))
            ),
            "module": error_type.__module__,
            "name": error_type.__name__,
        }

    if identifier.endswith("executable-error-instance"):
        observations = []
        for label, args in (
            ("empty", ()),
            ("single", ("runtime missing",)),
            ("multiple", ("runtime missing", 24)),
        ):
            value = error_type(*args)
            observations.append(
                {
                    "args": list(value.args),
                    "dictionary": dict(value.__dict__),
                    "label": label,
                    "repr": repr(value),
                    "str": str(value),
                }
            )
        return {"observations": observations}

    if not identifier.endswith("executable-error-raise"):
        raise RuntimeError(f"Unknown executable error case: {identifier}")
    caught: Exception | None = None
    try:
        raise error_type("runtime missing")
    except Exception as error:  # noqa: BLE001 - catchability is the fact.
        caught = error
    child_type = type("ClosedChild", (error_type,), {})
    child = child_type("child")
    child.marker = 7
    return {
        "caught_as_exception": isinstance(caught, Exception),
        "caught_as_exact_type": type(caught) is error_type,
        "child_caught_as_parent": isinstance(child, error_type),
        "dynamic_marker": child.marker,
        "separate_instances_equal": error_type("x") == error_type("x"),
        "subclassable": issubclass(child_type, error_type),
    }


def _logical_find_path(value: str) -> str:
    normalized = value.replace("\\", "/")
    name = normalized.rsplit("/", 1)[-1]
    if normalized.startswith("package-root/"):
        return f"package-root/{name}"
    if normalized.startswith("C:/"):
        return f"system-root/{name}"
    raise RuntimeError("find_executable_dir returned an unrecognized path.")


def _find_probe(
    module: Any,
    label: str,
    version: Any,
    package_names: list[str] | Exception,
    system_names: list[str] | Exception,
) -> dict[str, Any]:
    calls: list[str] = []

    def fake_listdir(path: Any) -> list[str]:
        token = "package-root" if str(path) == "package-root" else "system-root"
        calls.append(token)
        selected = package_names if token == "package-root" else system_names
        if isinstance(selected, Exception):
            raise selected
        return list(selected)

    with _patched(module.os, "listdir", fake_listdir):
        observation = _observe(
            label,
            lambda: module.find_executable_dir(version),
            normalize=_logical_find_path,
        )
    observation["listdir_calls"] = calls
    return observation


def _execute_find_executable(identifier: str, module: Any) -> dict[str, Any]:
    name_24 = "EnergyPlusV24-2-0"
    original_root = module.Directory.ENERGYPLUS_DIR
    module.Directory.ENERGYPLUS_DIR = Path("package-root")
    try:
        if identifier.endswith("find-executable-package-precedence"):
            return {
                "observations": [
                    _find_probe(
                        module,
                        "string",
                        "24.2",
                        [name_24],
                        [name_24],
                    ),
                    _find_probe(
                        module,
                        "list",
                        [24, 2],
                        [name_24],
                        [name_24],
                    ),
                    _find_probe(
                        module,
                        "version-instance",
                        module.Version(24, 2, 0),
                        [name_24],
                        [name_24],
                    ),
                ]
            }

        if identifier.endswith("find-executable-system-fallback"):
            return {
                "observations": [
                    _find_probe(
                        module,
                        "tuple",
                        (9, 6),
                        [],
                        ["EnergyPlusV9-6-0"],
                    ),
                    _find_probe(
                        module,
                        "string",
                        "8.9.0",
                        ["unrelated"],
                        ["EnergyPlusV8-9-0"],
                    ),
                ],
                "verification": "name-membership-only",
            }

        if not identifier.endswith("find-executable-failure"):
            raise RuntimeError(f"Unknown executable discovery case: {identifier}")
        return {
            "observations": [
                _find_probe(module, "not-found", "24.2", [], []),
                _find_probe(
                    module,
                    "package-listing-error",
                    "24.2",
                    FileNotFoundError("package runtime unavailable"),
                    [name_24],
                ),
                _find_probe(module, "invalid-version", "invalid", [], []),
            ]
        }
    finally:
        module.Directory.ENERGYPLUS_DIR = original_root


@contextmanager
def _closed_run_fakes(module: Any) -> Iterator[dict[str, Any]]:
    calls: list[dict[str, Any]] = []
    progress: list[dict[str, Any]] = []

    def fake_run_single(idfpath: str, weather: str, **kwargs: Any) -> str:
        calls.append(
            {
                "idf": idfpath,
                "kwargs": dict(kwargs),
                "weather": weather,
            }
        )
        if idfpath == "fail.idf":
            raise RuntimeError("closed fake stopped")
        return f"result:{idfpath}:{weather}"

    def fake_tqdm(iterator: Any, **kwargs: Any) -> Any:
        progress.append(dict(kwargs))
        return iterator

    with _patched(module, "run_single", fake_run_single), _patched(
        module, "tqdm", fake_tqdm
    ):
        yield {"calls": calls, "progress": progress}


def _execute_run(identifier: str, module: Any) -> dict[str, Any]:
    with _closed_run_fakes(module) as trace:
        if identifier.endswith("run-scalar"):
            value = module.run(
                "model.idf",
                "weather.epw",
                ep_dir="runtime-token",
                verbose=False,
                output_dir="output-token",
                delete=False,
            )
            list_value = module.run(["listed.idf"], ["listed.epw"])
            return {
                "calls": trace["calls"],
                "progress": trace["progress"],
                "returns": [value, list_value],
            }

        if identifier.endswith("run-broadcast"):
            idfs = ["model.idf"]
            weathers = ["first.epw", "second.epw"]
            first = module.run(
                idfs,
                weathers,
                ep_dir="runtime-token",
                verbose=True,
                output_dir="ignored-output-token",
                delete=False,
            )
            more_idfs = ["a.idf", "b.idf"]
            one_weather = ["only.epw"]
            second = module.run(more_idfs, one_weather)
            return {
                "caller_lists_after": {
                    "idfs": idfs,
                    "one_weather": one_weather,
                },
                "calls": trace["calls"],
                "progress": trace["progress"],
                "returns": [first, second],
            }

        if not identifier.endswith("run-cardinality"):
            raise RuntimeError(f"Unknown run case: {identifier}")
        observations = [
            _observe(
                "mismatch",
                lambda: module.run(
                    ["a.idf", "b.idf"],
                    ["1.epw", "2.epw", "3.epw"],
                ),
            ),
            _observe(
                "one-idf-no-weather",
                lambda: module.run(["a.idf"], []),
            ),
            _observe(
                "no-idf-one-weather",
                lambda: module.run([], ["one.epw"]),
            ),
            _observe(
                "short-circuit",
                lambda: module.run(
                    ["ok.idf", "fail.idf", "later.idf"],
                    ["1.epw", "2.epw", "3.epw"],
                ),
            ),
        ]
        return {
            "calls": trace["calls"],
            "observations": observations,
            "progress": trace["progress"],
        }


def _ensure_descendant(root: Path, value: Any) -> Path:
    candidate = Path(value).resolve()
    if candidate != root and root not in candidate.parents:
        raise RuntimeError("A launcher fake attempted to escape controlled temp.")
    return candidate


@contextmanager
def _closed_workspace(module: Any) -> Iterator[dict[str, Any]]:
    with tempfile.TemporaryDirectory(
        prefix="goniegonie-launcher-runtime-oracle-"
    ) as raw_root:
        root = Path(raw_root).resolve()
        created: list[Path] = []
        process_attempts: list[str] = []
        original_mkdtemp = module.tempfile.mkdtemp
        original_copy = module.shutil.copy
        original_rmtree = module.shutil.rmtree
        original_remove = module.os.remove
        original_run = module.subprocess.run
        original_popen = module.subprocess.Popen

        def fake_mkdtemp(*, prefix: str = "", **kwargs: Any) -> str:
            if kwargs or prefix != module.PackageInfo.NAME:
                raise RuntimeError("Unexpected temporary-directory request.")
            candidate = root / f"work-{len(created)}"
            candidate.mkdir()
            created.append(candidate)
            return str(candidate)

        def guarded_copy(source: Any, destination: Any, *args: Any, **kwargs: Any) -> str:
            _ensure_descendant(root, source)
            _ensure_descendant(root, destination)
            return str(original_copy(source, destination, *args, **kwargs))

        def guarded_rmtree(path: Any, *args: Any, **kwargs: Any) -> None:
            candidate = _ensure_descendant(root, path)
            if candidate == root:
                raise RuntimeError("The controlled root cannot be deleted by launcher code.")
            original_rmtree(path, *args, **kwargs)

        def guarded_remove(path: Any, *args: Any, **kwargs: Any) -> None:
            candidate = _ensure_descendant(root, path)
            if candidate == root:
                raise RuntimeError("The controlled root cannot be removed by launcher code.")
            original_remove(path, *args, **kwargs)

        def forbidden_process(*args: Any, **kwargs: Any) -> Any:
            del args, kwargs
            process_attempts.append("forbidden")
            raise RuntimeError("Process execution is forbidden in the launcher oracle.")

        module.tempfile.mkdtemp = fake_mkdtemp
        module.shutil.copy = guarded_copy
        module.shutil.rmtree = guarded_rmtree
        module.os.remove = guarded_remove
        module.subprocess.run = forbidden_process
        module.subprocess.Popen = forbidden_process
        try:
            yield {
                "created": created,
                "process_attempts": process_attempts,
                "root": root,
            }
            if process_attempts:
                raise RuntimeError("The launcher oracle attempted to execute a process.")
        finally:
            module.subprocess.Popen = original_popen
            module.subprocess.run = original_run
            module.os.remove = original_remove
            module.shutil.rmtree = original_rmtree
            module.shutil.copy = original_copy
            module.tempfile.mkdtemp = original_mkdtemp


def _write_controlled(root: Path, relative: str, text: str) -> Path:
    path = _ensure_descendant(root, root / relative)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


def _execute_run_single_explicit(module: Any, workspace: dict[str, Any]) -> dict[str, Any]:
    root: Path = workspace["root"]
    idf = _write_controlled(root, "inputs/explicit.idf", "no version object\n")
    output = root / "retained"
    output.mkdir()
    launch_calls: list[dict[str, Any]] = []
    resolver_calls: list[str] = []

    def forbidden_resolver(version: Any) -> str:
        resolver_calls.append(str(version))
        raise RuntimeError("Explicit runtime must bypass discovery.")

    def fake_launch(
        idfpath: str,
        weather: str,
        ep_dir: str,
        run_dir: str,
        *,
        verbose: bool,
    ) -> tuple[int, list[str]]:
        run_path = _ensure_descendant(root, run_dir)
        launch_calls.append(
            {
                "ep_dir": ep_dir,
                "idf": str(Path(idfpath).relative_to(root)).replace("\\", "/"),
                "run_dir": run_path.name,
                "verbose": verbose,
                "weather": weather,
            }
        )
        error = _write_controlled(
            root,
            f"{run_path.name}/case.err",
            "Elapsed Time=0hr 0min 1.5sec\n** Warning ** retained warning\n",
        )
        audit = _write_controlled(root, f"{run_path.name}/case.audit", "A= 1\n")
        return 73, [str(error), str(audit)]

    with _patched(module, "find_executable_dir", forbidden_resolver), _patched(
        module, "_launch_energyplus", fake_launch
    ):
        result = module.run_single(
            str(idf),
            "weather-token.epw",
            verbose=True,
            ep_dir="explicit-runtime",
            output_dir=str(output),
            delete=False,
        )
    return {
        "audit": result.audit,
        "copied_files": sorted(item.name for item in output.iterdir()),
        "elapsed_binary64": result.time.hex().removeprefix("0x"),
        "launch_calls": launch_calls,
        "launch_status_ignored": 73,
        "process_attempt_count": len(workspace["process_attempts"]),
        "resolver_calls": resolver_calls,
        "run_dir_exists_after": workspace["created"][0].exists(),
        "warnings": result.err.to_dict(orient="records"),
    }


def _execute_run_single_inferred(module: Any, workspace: dict[str, Any]) -> dict[str, Any]:
    root: Path = workspace["root"]
    idf = _write_controlled(root, "inputs/inferred.idf", "Version,123.2.0;\n")
    resolver_calls: list[str] = []
    launch_calls: list[dict[str, Any]] = []

    def fake_resolver(version: Any) -> str:
        resolver_calls.append(str(version))
        return "resolved-runtime"

    def fake_launch(
        idfpath: str,
        weather: str,
        ep_dir: str,
        run_dir: str,
        *,
        verbose: bool,
    ) -> tuple[int, list[str]]:
        del idfpath
        run_path = _ensure_descendant(root, run_dir)
        launch_calls.append(
            {
                "ep_dir": ep_dir,
                "run_dir": run_path.name,
                "verbose": verbose,
                "weather": weather,
            }
        )
        audit = _write_controlled(root, f"{run_path.name}/case.audit", "B= 2\n")
        return -9, [str(audit)]

    with _patched(module, "find_executable_dir", fake_resolver), _patched(
        module, "_launch_energyplus", fake_launch
    ):
        result = module.run_single(
            str(idf),
            "weather-token.epw",
            verbose=False,
            ep_dir=None,
            output_dir=None,
            delete=True,
        )
    return {
        "audit": result.audit,
        "copied_audit_exists_after": (idf.parent / "case.audit").exists(),
        "launch_calls": launch_calls,
        "launch_status_ignored": -9,
        "process_attempt_count": len(workspace["process_attempts"]),
        "resolver_calls": resolver_calls,
        "run_dir_exists_after": workspace["created"][0].exists(),
    }


def _execute_run_single_transactionality(
    module: Any, workspace: dict[str, Any]
) -> dict[str, Any]:
    root: Path = workspace["root"]
    inputs = root / "inputs"
    inputs.mkdir()
    missing_version = _write_controlled(root, "inputs/missing.idf", "Version,123;\n")
    valid = _write_controlled(root, "inputs/valid.idf", "Version,24.2;\n")
    output = root / "outputs"
    output.mkdir()

    before_missing = len(workspace["created"])
    missing_observation = _observe(
        "missing-version",
        lambda: module.run_single(str(missing_version), "weather", ep_dir=None),
        temp_root=root,
    )
    missing_created = len(workspace["created"]) != before_missing

    def raising_launch(*args: Any, **kwargs: Any) -> Any:
        del args, kwargs
        raise RuntimeError("closed launch failure")

    before_launch = len(workspace["created"])
    with _patched(module, "_launch_energyplus", raising_launch):
        launch_observation = _observe(
            "launch-failure",
            lambda: module.run_single(str(valid), "weather", ep_dir="runtime"),
            temp_root=root,
        )
    launch_dir = workspace["created"][before_launch]

    def missing_output_launch(
        idfpath: str,
        weather: str,
        ep_dir: str,
        run_dir: str,
        *,
        verbose: bool,
    ) -> tuple[int, list[str]]:
        del idfpath, weather, ep_dir, verbose
        run_path = _ensure_descendant(root, run_dir)
        return 0, [str(run_path / "missing.audit")]

    before_copy = len(workspace["created"])
    with _patched(module, "_launch_energyplus", missing_output_launch):
        copy_observation = _observe(
            "copy-failure",
            lambda: module.run_single(
                str(valid),
                "weather",
                ep_dir="runtime",
                output_dir=str(output),
            ),
            temp_root=root,
        )
    copy_dir = workspace["created"][before_copy]

    def malformed_output_launch(
        idfpath: str,
        weather: str,
        ep_dir: str,
        run_dir: str,
        *,
        verbose: bool,
    ) -> tuple[int, list[str]]:
        del idfpath, weather, ep_dir, verbose
        run_path = _ensure_descendant(root, run_dir)
        bad = _write_controlled(root, f"{run_path.name}/bad.err", "no elapsed marker")
        return 0, [str(bad)]

    before_parse = len(workspace["created"])
    with _patched(module, "_launch_energyplus", malformed_output_launch):
        parse_observation = _observe(
            "parse-failure",
            lambda: module.run_single(
                str(valid),
                "weather",
                ep_dir="runtime",
                output_dir=str(output),
                delete=True,
            ),
            temp_root=root,
        )
    parse_dir = workspace["created"][before_parse]
    return {
        "observations": [
            missing_observation,
            launch_observation,
            copy_observation,
            parse_observation,
        ],
        "process_attempt_count": len(workspace["process_attempts"]),
        "side_effects": {
            "copy_failure_run_dir_exists": copy_dir.exists(),
            "launch_failure_run_dir_exists": launch_dir.exists(),
            "missing_version_created_run_dir": missing_created,
            "parse_failure_copied_output_exists": (output / "bad.err").exists(),
            "parse_failure_run_dir_exists": parse_dir.exists(),
        },
    }


def _execute_run_single(identifier: str, module: Any) -> dict[str, Any]:
    with _closed_workspace(module) as workspace:
        if identifier.endswith("run-single-explicit-retain"):
            return _execute_run_single_explicit(module, workspace)
        if identifier.endswith("run-single-inferred-delete"):
            return _execute_run_single_inferred(module, workspace)
        if identifier.endswith("run-single-transactionality"):
            return _execute_run_single_transactionality(module, workspace)
        raise RuntimeError(f"Unknown run_single case: {identifier}")


EXECUTORS: dict[str, Callable[[str, Any], dict[str, Any]]] = {
    "executable-error": _execute_executable_error,
    "find-executable": _execute_find_executable,
    "run": _execute_run,
    "run-single": _execute_run_single,
}


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _dependencies() -> dict[str, str]:
    return BASE._dependencies()


def build_oracle(
    inventory: dict[str, Any],
    commit: str,
    source: Path | None = None,
) -> dict[str, Any]:
    imported_source = (
        source.resolve() if source is not None else BASE._find_pinned_source()
    )
    imported_sha256 = sha256_file(imported_source)
    if imported_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported launcher.py is not the inventoried source.")
    definitions = case_definitions()
    with BASE._pinned_launcher(imported_source) as module:
        if any(not hasattr(module, symbol) for symbol in TARGET_SYMBOLS):
            raise SystemExit("The pinned launcher runtime symbol surface drifted.")
        cases = [
            {
                "executor": definition["executor"],
                "expected_dotnet": definition["expected_dotnet"],
                "id": definition["id"],
                "python": {
                    "facts": EXECUTORS[definition["executor"]](
                        definition["id"], module
                    ),
                    "outcome": "returned",
                },
                "symbol": definition["symbol"],
            }
            for definition in definitions
        ]
    result = {
        "cases": cases,
        "cases_sha256": cases_sha256(cases),
        "consumer_contract": {
            "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
            "assertion_ids": EXPECTED_ASSERTION_IDS,
            "case_count": EXPECTED_CASE_COUNT,
            "case_ids": list(EXPECTED_CASE_IDS),
            "classifications": {symbol: "exception" for symbol in TARGET_SYMBOLS},
            "execution_policy": "closed-fakes-no-process-or-active-load",
            "float_encoding": "python-binary64-hex-without-0x-prefix",
            "path_encoding": "logical-tokens-only",
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


def _case_by_id(value: dict[str, Any], identifier: str) -> dict[str, Any]:
    matches = [item for item in value["cases"] if item["id"] == identifier]
    if len(matches) != 1:
        raise RuntimeError(f"Expected exactly one case {identifier!r}.")
    return matches[0]


def _validate_observation(value: Any, location: str) -> None:
    if not isinstance(value, dict):
        raise RuntimeError(f"{location} is not an observation.")
    if value.get("outcome") == "returned":
        required = {"label", "outcome", "result"}
    else:
        required = {
            "error_category",
            "exception_type",
            "label",
            "message",
            "outcome",
        }
        if value.get("outcome") != "raised":
            raise RuntimeError(f"{location} outcome drifted.")
    optional = {"listdir_calls"}
    if not required.issubset(value) or not set(value).issubset(required | optional):
        raise RuntimeError(f"{location} observation shape drifted.")


def _validate_semantics(value: dict[str, Any]) -> None:
    error_class = _case_by_id(
        value, "launcher-runtime.executable-error-class"
    )["python"]["facts"]
    if (
        error_class["base_names"] != ["Exception"]
        or error_class["direct_dictionary_keys"]
        != ["__doc__", "__module__", "__weakref__"]
        or error_class["inspect_signature"]["exception_type"] != "ValueError"
    ):
        raise RuntimeError("Executable error class semantics drifted.")

    error_instance = _case_by_id(
        value, "launcher-runtime.executable-error-instance"
    )["python"]["facts"]["observations"]
    if [item["str"] for item in error_instance] != [
        "",
        "runtime missing",
        "('runtime missing', 24)",
    ]:
        raise RuntimeError("Executable error instance semantics drifted.")

    error_raise = _case_by_id(
        value, "launcher-runtime.executable-error-raise"
    )["python"]["facts"]
    if error_raise != {
        "caught_as_exception": True,
        "caught_as_exact_type": True,
        "child_caught_as_parent": True,
        "dynamic_marker": 7,
        "separate_instances_equal": False,
        "subclassable": True,
    }:
        raise RuntimeError("Executable error raise semantics drifted.")

    package = _case_by_id(
        value, "launcher-runtime.find-executable-package-precedence"
    )["python"]["facts"]["observations"]
    if any(
        item.get("outcome") != "returned"
        or item.get("listdir_calls") != ["package-root"]
        or item.get("result") != "package-root/EnergyPlusV24-2-0"
        for item in package
    ):
        raise RuntimeError("Package runtime precedence drifted.")

    fallback = _case_by_id(
        value, "launcher-runtime.find-executable-system-fallback"
    )["python"]["facts"]
    if fallback["verification"] != "name-membership-only" or any(
        item.get("listdir_calls") != ["package-root", "system-root"]
        or not str(item.get("result", "")).startswith("system-root/")
        for item in fallback["observations"]
    ):
        raise RuntimeError("System runtime fallback drifted.")

    discovery_failures = _case_by_id(
        value, "launcher-runtime.find-executable-failure"
    )["python"]["facts"]["observations"]
    if (
        [item["exception_type"] for item in discovery_failures]
        != [
            "ExecutableEnergyPlusNotFoundError",
            "FileNotFoundError",
            "ValueError",
        ]
        or discovery_failures[0]["message"]
        != "EnergyPlus 버전 맞는걸로 안깔려있는듯"
        or discovery_failures[2]["listdir_calls"] != []
    ):
        raise RuntimeError("Runtime discovery failure surface drifted.")

    broadcast = _case_by_id(value, "launcher-runtime.run-broadcast")["python"][
        "facts"
    ]
    if (
        broadcast["caller_lists_after"]
        != {
            "idfs": ["model.idf", "model.idf"],
            "one_weather": ["only.epw", "only.epw"],
        }
        or len(broadcast["calls"]) != 4
        or any(call["kwargs"].get("verbose") is not False for call in broadcast["calls"])
        or any("output_dir" in call["kwargs"] for call in broadcast["calls"])
        or broadcast["progress"]
        != [
            {"desc": "Running idfs", "ncols": 100},
            {"desc": "Running idfs", "ncols": 100},
        ]
    ):
        raise RuntimeError("Run broadcast semantics drifted.")

    cardinality = _case_by_id(value, "launcher-runtime.run-cardinality")[
        "python"
    ]["facts"]
    if [item["outcome"] for item in cardinality["observations"]] != [
        "raised",
        "raised",
        "returned",
        "raised",
    ] or [item.get("exception_type") for item in cardinality["observations"]] != [
        "ValueError",
        "IndexError",
        None,
        "RuntimeError",
    ]:
        raise RuntimeError("Run cardinality semantics drifted.")

    scalar = _case_by_id(value, "launcher-runtime.run-scalar")["python"]["facts"]
    if len(scalar["calls"]) != 2 or scalar["progress"] != []:
        raise RuntimeError("Run scalar dispatch drifted.")
    if scalar["calls"][0]["kwargs"] != {
        "delete": False,
        "ep_dir": "runtime-token",
        "output_dir": "output-token",
        "verbose": False,
    }:
        raise RuntimeError("Run scalar keyword forwarding drifted.")

    explicit = _case_by_id(
        value, "launcher-runtime.run-single-explicit-retain"
    )["python"]["facts"]
    if (
        explicit["resolver_calls"] != []
        or explicit["copied_files"] != ["case.audit", "case.err"]
        or explicit["run_dir_exists_after"]
        or explicit["process_attempt_count"] != 0
        or explicit["launch_status_ignored"] != 73
    ):
        raise RuntimeError("Explicit single-run retention drifted.")

    inferred = _case_by_id(
        value, "launcher-runtime.run-single-inferred-delete"
    )["python"]["facts"]
    if (
        inferred["resolver_calls"] != ["23.2.0"]
        or inferred["copied_audit_exists_after"]
        or inferred["run_dir_exists_after"]
        or inferred["process_attempt_count"] != 0
        or inferred["launch_status_ignored"] != -9
    ):
        raise RuntimeError("Inferred single-run deletion drifted.")

    transaction = _case_by_id(
        value, "launcher-runtime.run-single-transactionality"
    )["python"]["facts"]
    if [item["exception_type"] for item in transaction["observations"]] != [
        "RuntimeError",
        "RuntimeError",
        "FileNotFoundError",
        "AttributeError",
    ] or transaction["side_effects"] != {
        "copy_failure_run_dir_exists": True,
        "launch_failure_run_dir_exists": True,
        "missing_version_created_run_dir": False,
        "parse_failure_copied_output_exists": True,
        "parse_failure_run_dir_exists": False,
    }:
        raise RuntimeError("Single-run transactionality drifted.")

    for case in value["cases"]:
        facts = case["python"]["facts"]
        if "observations" in facts and case["id"] not in {
            "launcher-runtime.executable-error-instance"
        }:
            for index, observation in enumerate(facts["observations"]):
                _validate_observation(observation, f"{case['id']}[{index}]")


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if type(value) is float:
        raise RuntimeError(f"Raw float entered {location}.")
    if isinstance(value, Path):
        raise RuntimeError(f"Raw path entered {location}.")
    if isinstance(value, str):
        if RAW_ADDRESS_PATTERN.search(value):
            raise RuntimeError(f"A raw address entered {location}.")
        if ABSOLUTE_PATH_PATTERN.search(value):
            raise RuntimeError(f"An absolute path entered {location}.")
        if TEMP_LEAK_PATTERN.search(value):
            raise RuntimeError(f"A controlled temporary token entered {location}.")
        if GUID_PATTERN.search(value):
            raise RuntimeError(f"A GUID-like token entered {location}.")
        if TIMESTAMP_PATTERN.search(value):
            raise RuntimeError(f"A timestamp entered {location}.")
        return
    if value is None or type(value) in (bool, int):
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_safe_tree(item, f"{location}[{index}]")
        return
    if isinstance(value, dict):
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"A non-string key entered {location}.")
            _validate_safe_tree(item, f"{location}.{key}")
        return
    raise RuntimeError(f"Raw object {type(value).__name__} entered {location}.")


def validate_oracle(value: dict[str, Any]) -> None:
    _require_keys(
        value,
        {
            "cases",
            "cases_sha256",
            "consumer_contract",
            "runtime",
            "schema",
            "symbols",
            "upstream",
        },
        "root",
    )
    if value["schema"] != SCHEMA:
        raise RuntimeError("Launcher runtime schema drifted.")
    definitions = case_definitions()
    if len(value["cases"]) != EXPECTED_CASE_COUNT or [
        item["id"] for item in value["cases"]
    ] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Launcher runtime case order/count drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Launcher runtime cases hash drifted.")
    by_id = {item["id"]: item for item in definitions}
    for case in value["cases"]:
        _require_keys(
            case,
            {"executor", "expected_dotnet", "id", "python", "symbol"},
            f"case {case.get('id')!r}",
        )
        definition = by_id[case["id"]]
        if any(
            case[key] != definition[key]
            for key in ("executor", "expected_dotnet", "symbol")
        ):
            raise RuntimeError(f"Case contract drifted: {case['id']}")
        _require_keys(case["expected_dotnet"], {"adaptation", "outcome"}, "native")
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if case["python"]["outcome"] != "returned" or not case["python"]["facts"]:
            raise RuntimeError(f"Python case outcome drifted: {case['id']}")

    expected_contract = {
        "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        "assertion_ids": EXPECTED_ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": {symbol: "exception" for symbol in TARGET_SYMBOLS},
        "execution_policy": "closed-fakes-no-process-or-active-load",
        "float_encoding": "python-binary64-hex-without-0x-prefix",
        "path_encoding": "logical-tokens-only",
        "runtime_names": "pinned-python-only-no-native-type-name-claims",
        "target_symbols": list(TARGET_SYMBOLS),
    }
    if value["consumer_contract"] != expected_contract:
        raise RuntimeError("Launcher runtime consumer contract drifted.")
    expected_runtime = {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }
    if value["runtime"] != expected_runtime:
        raise RuntimeError("Launcher runtime pin drifted.")
    if value["upstream"] != {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    }:
        raise RuntimeError("Launcher runtime upstream receipt drifted.")
    expected_symbols = [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]
    if value["symbols"] != expected_symbols:
        raise RuntimeError("Launcher runtime symbol receipts drifted.")
    _validate_semantics(value)
    _validate_safe_tree(value)
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the launcher runtime oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
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
    serialized = strict_json_dumps(result, indent=2) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(serialized, encoding="utf-8", newline="\n")
    print(f"Wrote launcher runtime oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

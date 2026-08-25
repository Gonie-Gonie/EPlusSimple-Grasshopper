from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import shutil
import sys
import uuid


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
RUNNER_PATH = REPOSITORY_ROOT / "tools" / "compatibility-runner" / "compare_outputs.py"


def _load_runner():
    spec = importlib.util.spec_from_file_location(
        "goniegonie_compatibility_compare_outputs",
        RUNNER_PATH,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load compatibility runner: {RUNNER_PATH}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


runner = _load_runner()


class TemporaryWorkspace:
    def __init__(self) -> None:
        base = REPOSITORY_ROOT / "temp" / "tests" / "compatibility-runner"
        base.mkdir(parents=True, exist_ok=True)
        self.path = base / uuid.uuid4().hex
        self.path.mkdir()

    def __enter__(self) -> "TemporaryWorkspace":
        return self

    def __exit__(self, exception_type, exception, traceback) -> None:
        resolved = self.path.resolve()
        expected = (
            REPOSITORY_ROOT / "temp" / "tests" / "compatibility-runner"
        ).resolve()
        resolved.relative_to(expected)
        shutil.rmtree(resolved)

    def write_text(self, relative: str, text: str) -> Path:
        target = self.path.joinpath(*relative.split("/"))
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8", newline="\n")
        return target

    def write_json(self, relative: str, value: object) -> Path:
        return self.write_text(
            relative,
            json.dumps(value, ensure_ascii=False, indent=2) + "\n",
        )


def metadata(seed: str = "a") -> dict[str, object]:
    hashes = {
        "grm": seed * 64,
        "weather": ("b" if seed == "a" else seed) * 64,
        "energyplus": ("c" if seed == "a" else seed) * 64,
        "idd": ("d" if seed == "a" else seed) * 64,
        "expandobjects": ("e" if seed == "a" else seed) * 64,
    }
    return {
        "inputs": {
            "grm": {"sha256": hashes["grm"]},
            "weather": {"sha256": hashes["weather"]},
        },
        "runtime": {
            "energyplus_exe_sha256": hashes["energyplus"],
            "idd_sha256": hashes["idd"],
            "expandobjects_sha256": hashes["expandobjects"],
        },
    }


def manifest() -> dict[str, object]:
    return {
        "tolerances": {
            "idf_absolute": 1e-6,
            "idf_relative": 1e-4,
            "grr_absolute": 0.01,
            "grr_relative": 0.001,
            "near_zero": 0.005,
            "warning_count_delta": 0,
        }
    }

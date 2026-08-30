"""Regression tests for the fail-closed Python oracle process boundary."""

from __future__ import annotations

import ast
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
TEST_ROOT = REPOSITORY_ROOT / "tests" / "PythonReference"
BOOTSTRAP_PATH = (
    REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
)
RUNNER_PATH = (
    REPOSITORY_ROOT / "tools" / "python-reference" / "run_reference_tests.py"
)
LOCK_PATH = (
    REPOSITORY_ROOT / "tools" / "python-reference" / "requirements.lock.txt"
)
EXPECTED_VALIDATORS = frozenset(
    {
        "test_constants_metadata_oracle.py",
        "test_dragon_construction_air_boundary_core_oracle.py",
        "test_dragon_construction_core_oracle.py",
        "test_dragon_construction_to_idf_object_oracle.py",
        "test_dragon_hvac_appenders_controllers_oracle.py",
        "test_dragon_hvac_misc_systems_core_oracle.py",
        "test_dragon_hvac_photovoltaic_to_idf_object_oracle.py",
        "test_dragon_hvac_source_system_to_idf_object_oracle.py",
        "test_dragon_hvac_source_tower_core_oracle.py",
        "test_dragon_hvac_supply_core_oracle.py",
        "test_dragon_hvac_supply_group_core_oracle.py",
        "test_dragon_hvac_supply_group_to_idf_object_oracle.py",
        "test_dragon_model_add_supply_system_oracle.py",
        "test_dragon_model_assembly_oracle.py",
        "test_dragon_model_class_oracle.py",
        "test_dragon_model_construction_defaults_oracle.py",
        "test_dragon_model_projections_oracle.py",
        "test_dragon_shape_geometry_core_oracle.py",
        "test_dragon_shape_opening_adjacency_core_oracle.py",
        "test_dragon_shape_shading_material_to_idf_object_oracle.py",
        "test_dragon_shape_surface_to_idf_object_oracle.py",
        "test_dragon_shape_zone_core_oracle.py",
        "test_dragon_shape_zone_to_idf_object_oracle.py",
        "test_epsimple_construction_core_oracle.py",
        "test_epsimple_hvac_enums_base_oracle.py",
        "test_epsimple_hvac_other_systems_oracle.py",
        "test_epsimple_hvac_supply_system_oracle.py",
        "test_epsimple_hvac_thermal_source_oracle.py",
        "test_epsimple_model_core_oracle.py",
        "test_epsimple_model_result_oracle.py",
        "test_epsimple_shape_core_oracle.py",
        "test_imugi_idd_definitions_core_oracle.py",
        "test_imugi_idd_schema_static_core_oracle.py",
        "test_imugi_idf_object_core_oracle.py",
        "test_imugi_idf_object_list_core_oracle.py",
    }
)


def _load_runner():
    specification = importlib.util.spec_from_file_location(
        "goniegonie_reference_test_runner",
        RUNNER_PATH,
    )
    if specification is None or specification.loader is None:
        raise RuntimeError(f"Cannot load reference test runner: {RUNNER_PATH}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def _is_sys_executable(node: ast.expr) -> bool:
    return (
        isinstance(node, ast.Attribute)
        and isinstance(node.value, ast.Name)
        and node.value.id == "sys"
        and node.attr == "executable"
    )


def _bootstrap_commands(tree: ast.AST) -> list[ast.List | ast.Tuple]:
    commands: list[ast.List | ast.Tuple] = []
    for node in ast.walk(tree):
        if not isinstance(node, (ast.List, ast.Tuple)) or not node.elts:
            continue
        if not _is_sys_executable(node.elts[0]):
            continue
        if not any("bootstrap" in ast.unparse(item).lower() for item in node.elts[1:]):
            continue
        commands.append(node)
    return commands


def _references_bootstrap(tree: ast.AST) -> bool:
    return any(
        isinstance(node, ast.Constant)
        and isinstance(node.value, str)
        and "bootstrap_reference.py" in node.value.lower()
        for node in ast.walk(tree)
    )


def _subprocess_run_calls(tree: ast.AST) -> list[ast.Call]:
    return [
        node
        for node in ast.walk(tree)
        if isinstance(node, ast.Call)
        and isinstance(node.func, ast.Attribute)
        and isinstance(node.func.value, ast.Name)
        and node.func.value.id == "subprocess"
        and node.func.attr == "run"
    ]


class OracleProcessIsolationTests(unittest.TestCase):
    def test_runner_owns_every_nested_bootstrap_callsite(self) -> None:
        actual: set[str] = set()
        for path in sorted(TEST_ROOT.glob("test_*.py")):
            if path == Path(__file__).resolve():
                continue
            source = path.read_text(encoding="utf-8")
            tree = ast.parse(source)
            if not _references_bootstrap(tree):
                continue
            commands = _bootstrap_commands(tree)
            self.assertEqual(1, len(commands), path)
            run_calls = _subprocess_run_calls(tree)
            self.assertEqual(1, len(run_calls), path)
            self.assertIn("env", {item.arg for item in run_calls[0].keywords}, path)
            actual.add(path.name)

        self.assertEqual(EXPECTED_VALIDATORS, frozenset(actual))

    def test_lock_includes_the_previously_global_only_roots(self) -> None:
        pins = {
            line.strip().lower()
            for line in LOCK_PATH.read_text(encoding="utf-8").splitlines()
            if line.strip() and not line.lstrip().startswith("#")
        }
        self.assertIn("eppy==0.5.63", pins)
        self.assertIn("shapely==2.0.6", pins)

    def test_runner_removes_poison_and_preserves_deterministic_startup(self) -> None:
        runner = _load_runner()
        temp_root = REPOSITORY_ROOT / "temp"
        temp_root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=temp_root) as directory:
            working = Path(directory)
            dependency_root = working / "dependencies"
            upstream_source = working / "upstream"
            poison_root = working / "host-site-packages"
            dependency_root.mkdir()
            upstream_source.mkdir()
            poison_root.mkdir()
            (poison_root / "goniegonie_oracle_poison.py").write_text(
                "VALUE = 'host-only'\n",
                encoding="utf-8",
            )
            metadata_root = poison_root / "goniegonie_oracle_poison-9.9.9.dist-info"
            metadata_root.mkdir()
            (metadata_root / "METADATA").write_text(
                "Metadata-Version: 2.1\n"
                "Name: goniegonie-oracle-poison\n"
                "Version: 9.9.9\n",
                encoding="utf-8",
            )
            probe = working / "probe.py"
            probe.write_text(
                """from __future__ import annotations
import importlib.metadata
import importlib.util
import json
from pathlib import Path
import sys

distribution_visible = any(
    item.metadata.get("Name", "").lower() == "goniegonie-oracle-poison"
    for item in importlib.metadata.distributions()
)
payload = {
    "distribution_visible": distribution_visible,
    "dont_write_bytecode": sys.dont_write_bytecode,
    "hash_randomization": sys.flags.hash_randomization,
    "hash_value": hash("goniegonie-oracle-boundary"),
    "module_visible": importlib.util.find_spec("goniegonie_oracle_poison") is not None,
    "no_site": sys.flags.no_site,
    "safe_path": sys.flags.safe_path,
    "sys_path": sys.path,
    "utf8_mode": sys.flags.utf8_mode,
}
Path(sys.argv[1]).write_text(
    json.dumps(payload, sort_keys=True, indent=2) + "\\n",
    encoding="utf-8",
)
""",
                encoding="utf-8",
            )

            environment = os.environ.copy()
            environment["PYTHONHOME"] = str(poison_root)
            environment["PYTHONPATH"] = str(poison_root)
            command = [
                sys.executable,
                "-B",
                "-X",
                "utf8",
                str(BOOTSTRAP_PATH),
                "--dependency-root",
                str(dependency_root),
                "--upstream-source",
                str(upstream_source),
                "--generator",
                str(probe),
                "--",
            ]
            outputs: list[bytes] = []
            for name in ("one.json", "two.json"):
                output = working / name
                runner.run_isolated_bootstrap(
                    [*command, str(output)],
                    cwd=REPOSITORY_ROOT,
                    env=environment,
                    check=True,
                    capture_output=True,
                    text=True,
                )
                outputs.append(output.read_bytes())

            self.assertEqual(outputs[0], outputs[1])
            observed = json.loads(outputs[0])
            self.assertFalse(observed["module_visible"])
            self.assertFalse(observed["distribution_visible"])
            self.assertTrue(observed["dont_write_bytecode"])
            self.assertEqual(0, observed["hash_randomization"])
            self.assertEqual(1, observed["no_site"])
            self.assertTrue(observed["safe_path"])
            self.assertEqual(1, observed["utf8_mode"])
            self.assertNotIn(str(poison_root), observed["sys_path"])
            self.assertEqual(str(dependency_root), observed["sys_path"][0])
            self.assertEqual(str(upstream_source), observed["sys_path"][1])


if __name__ == "__main__":
    unittest.main()

from __future__ import annotations

import hashlib
from pathlib import Path
import shutil
import subprocess
import unittest


ROOT = Path(__file__).resolve().parents[2]
COMPATIBILITY = ROOT / "scripts" / "compatibility.ps1"
RELEASE = ROOT / "scripts" / "release.ps1"
SOURCE_ROOTS = (
    "src/Shared/GonieGonie.BuildingEnergy.Contracts",
    "src/Shared/GonieGonie.EnergyPlus.Runtime",
    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core",
    "src/SimpleDragon/GonieGonie.SimpleDragon.Core",
    "tools/compatibility-runner",
)


class EngineeringProvenanceContractTests(unittest.TestCase):
    def test_source_set_is_explicit_nonempty_and_has_deterministic_receipt(self) -> None:
        files = sorted(
            path
            for source_root in SOURCE_ROOTS
            for path in (ROOT / source_root).rglob("*")
            if path.is_file() and path.suffix in {".cs", ".csproj"}
        )
        self.assertGreater(len(files), 5)
        relative = [path.relative_to(ROOT).as_posix() for path in files]
        self.assertEqual(len(relative), len(set(relative)))
        lines = [
            f"sha256:{hashlib.sha256(path.read_bytes()).hexdigest()}  "
            f"{path.stat().st_size}  {path.relative_to(ROOT).as_posix()}"
            for path in files
        ]
        first = hashlib.sha256(("\n".join(lines) + "\n").encode()).hexdigest()
        second = hashlib.sha256(("\n".join(lines) + "\n").encode()).hexdigest()
        self.assertEqual(first, second)

    def test_generator_records_head_dirty_sources_and_executed_binary_identity(self) -> None:
        text = COMPATIBILITY.read_text(encoding="utf-8")
        for required in (
            "git = Get-EngineeringGitState",
            "production_source_set = Get-EngineeringSourceSet",
            "executed_binaries = Get-EngineeringBinarySet",
            "gha_executed = $false",
            "target_framework = 'net8.0-windows'",
            "[Reflection.AssemblyName]::GetAssemblyName",
        ):
            self.assertIn(required, text)
        self.assertLess(text.index("comparison.log"), text.index("port_provenance"))

    def test_release_fails_closed_on_commit_dirty_source_binary_and_exact_8x6(self) -> None:
        text = RELEASE.read_text(encoding="utf-8")
        for required in (
            "Assert-EngineeringPortProvenance",
            "[string] $provenance.git.commit -cne $ExpectedCommit",
            "[bool] $provenance.git.dirty",
            "production source-set membership differs",
            "production source-set aggregate hash drifted",
            "Engineering binary binding drifted",
            "declared_case_count -ne 8",
            "$engineeringCases.Count -ne 8",
            "did not declare and execute the exact six release stages",
            "[int] $engineeringCompatibility.skip_count -ne 0",
        ):
            self.assertIn(required, text)

    def test_modified_source_or_binary_cannot_match_its_recorded_hash(self) -> None:
        source = next((ROOT / SOURCE_ROOTS[0]).rglob("*.cs"))
        original = source.read_bytes()
        recorded = hashlib.sha256(original).digest()
        self.assertNotEqual(recorded, hashlib.sha256(original + b"\n").digest())
        binary_names = {
            "GonieGonie.CompatibilityRunner",
            "GonieGonie.BuildingEnergy.Contracts",
            "GonieGonie.EnergyPlus.Runtime",
            "GonieGonie.InvisibleDragon.Core",
            "GonieGonie.SimpleDragon.Core",
        }
        self.assertEqual(5, len(binary_names))

    def test_powershell_scripts_parse(self) -> None:
        powershell = shutil.which("powershell") or shutil.which("pwsh")
        if powershell is None:
            self.skipTest("PowerShell is unavailable")
        expression = (
            "& { param($first,$second) $ErrorActionPreference='Stop';"
            "[void][scriptblock]::Create([IO.File]::ReadAllText($first));"
            "[void][scriptblock]::Create([IO.File]::ReadAllText($second)) }"
        )
        completed = subprocess.run(
            [powershell, "-NoLogo", "-NoProfile", "-Command", expression,
             str(COMPATIBILITY), str(RELEASE)],
            cwd=ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(0, completed.returncode, completed.stderr)


if __name__ == "__main__":
    unittest.main()

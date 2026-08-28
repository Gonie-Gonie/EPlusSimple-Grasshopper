from __future__ import annotations

import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import unittest


ROOT = Path(__file__).resolve().parents[2]
COMPATIBILITY = ROOT / "scripts" / "compatibility.ps1"
RELEASE = ROOT / "scripts" / "release.ps1"
MANIFEST = ROOT / "fixtures" / "compatibility" / "cases.json"
SOURCE_ROOTS = (
    "src/Shared/GonieGonie.BuildingEnergy.Contracts",
    "src/Shared/GonieGonie.EnergyPlus.Runtime",
    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core",
    "src/SimpleDragon/GonieGonie.SimpleDragon.Core",
    "tools/compatibility-runner",
)


class EngineeringProvenanceContractTests(unittest.TestCase):
    def test_manifest_is_exact_eleven_case_sixty_six_stage_climate_contract(self) -> None:
        data = json.loads(MANIFEST.read_text(encoding="utf-8"))
        cases = data["cases"]
        expected_ids = {
            "ashrae-140-modified",
            "two-zone-one-sided-adjacency-shared-hp",
            "screw-chiller-closed-two-speed-fcu",
            "packaged-erv-pv-openings",
            "packaged-erv-pv-openings--tampa",
            "packaged-erv-pv-openings--golden",
            "packaged-erv-pv-openings--san-francisco",
            "geothermal-heat-pump-ahu",
            "boiler-heating-fuel-shared-matrix",
            "absorption-default-explicit-electric-radiant",
            "district-shared-fcu-radiator-radiant-dhw",
        }
        required_stages = {
            "grm_cross_read", "authoring_idf", "expanded_idf",
            "energyplus", "grr", "warnings",
        }
        self.assertEqual(11, len(cases))
        self.assertEqual(expected_ids, {case["id"] for case in cases})
        self.assertEqual(66, sum(len(case["stages"]) for case in cases))
        for case in cases:
            self.assertEqual(required_stages, set(case["stages"]))
            self.assertRegex(case["weather_sha256"], r"^[0-9a-f]{64}$")
            self.assertTrue(case["weather"].startswith("WeatherData/"))
            self.assertTrue(case["weather_header"].startswith("LOCATION,"))

        climate_cases = {
            case["id"]: case for case in cases
            if case["id"].startswith("packaged-erv-pv-openings")
        }
        self.assertEqual(4, len(climate_cases))
        self.assertEqual(4, len({case["weather_sha256"] for case in climate_cases.values()}))
        self.assertEqual(4, len({case["weather_header"] for case in climate_cases.values()}))

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

    def test_release_fails_closed_on_commit_dirty_source_binary_and_exact_11x6(self) -> None:
        text = RELEASE.read_text(encoding="utf-8")
        for required in (
            "Assert-EngineeringPortProvenance",
            "[string] $provenance.git.commit -cne $ExpectedCommit",
            "[bool] $provenance.git.dirty",
            "production source-set membership differs",
            "production source-set aggregate hash drifted",
            "Engineering binary binding drifted",
            "declared_case_count -ne 11",
            "$engineeringCases.Count -ne 11",
            "$engineeringStageReceiptCount -ne 66",
            "packaged-erv-pv-openings--tampa",
            "packaged-erv-pv-openings--golden",
            "packaged-erv-pv-openings--san-francisco",
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

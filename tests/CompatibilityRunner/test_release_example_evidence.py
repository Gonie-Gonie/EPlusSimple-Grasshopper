from __future__ import annotations

from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[2]
RELEASE = ROOT / "scripts" / "release.ps1"
RELEASE_WORKFLOW = ROOT / ".github" / "workflows" / "release-candidate.yml"
RELEASE_CHECKLIST = ROOT / "docs" / "release-checklist.md"


class ReleaseExampleEvidenceContractTests(unittest.TestCase):
    def test_release_selects_one_new_example_run_and_copies_only_four_files(self) -> None:
        text = RELEASE.read_text(encoding="utf-8")
        for required in (
            "$exampleRunsBeforeGate = @(Get-ExampleGateRunPaths)",
            "$exampleRun = Find-ExampleGateRun -ExistingPaths $exampleRunsBeforeGate",
            "Expected exactly one new Grasshopper example-gate run",
            "'v7/summary.json'",
            "'v8/summary.json'",
            "'PASS.txt'",
            "'ENERGYPLUS-WORKFLOW-PASS.txt'",
            "$actualEvidenceFiles.Count -ne 4",
            "grasshopperExampleGate = $exampleGateEvidence",
            "Get-ChildItem -LiteralPath $exampleEvidenceRoot -File",
        ):
            self.assertIn(required, text)

        copy_start = text.index("function Copy-ExampleGateEvidence")
        copy_end = text.index("function Get-PortableHostGateRunPaths", copy_start)
        copy_contract = text[copy_start:copy_end]
        self.assertNotIn("*.epw", copy_contract.lower())
        self.assertNotIn("Copy-Item -LiteralPath $runFull", copy_contract)

    def test_release_binds_copied_summaries_markers_and_hashes(self) -> None:
        release = RELEASE.read_text(encoding="utf-8")
        workflow = RELEASE_WORKFLOW.read_text(encoding="utf-8")
        checklist = RELEASE_CHECKLIST.read_text(encoding="utf-8")
        for required in (
            "goniegonie.dragons-grasshopper.examples.v3",
            "does not cover every tracked definition exactly once",
            "does not cover every tracked Rhino model exactly once",
            "runtimeGateStatus -cne 'ready'",
            "Get-Sha256 -Path $reportPath",
            "Get-Sha256 -Path $markerPath",
        ):
            self.assertIn(required, release)

        for required in (
            "$exampleGate = $gate.verification.grasshopperExampleGate",
            "Assert-ExampleEvidenceFile",
            "release/grasshopper-example-gate/rhino7-summary.json",
            "release/grasshopper-example-gate/rhino8-summary.json",
            "$checksumLines -notcontains",
        ):
            self.assertIn(required, workflow)
        self.assertIn("`grasshopper-example-gate`", checklist)


if __name__ == "__main__":
    unittest.main()

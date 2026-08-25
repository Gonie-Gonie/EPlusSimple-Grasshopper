from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import unittest

from support import REPOSITORY_ROOT, TemporaryWorkspace


SESSION_ID = "2" * 32
REPOSITORY_HEAD = "3" * 40
UPSTREAM_COMMIT = "4" * 40
NONCE = "5" * 64
PROJECT_PATH = "tests/Product.Tests/Product.Tests.csproj"
PROJECT_SLUG = "abc123def456"
ASSERTION_ID = "service-parity"
HASH_A = "sha256:" + "a" * 64
HASH_B = "sha256:" + "b" * 64
HASH_C = "sha256:" + "c" * 64


def _sha256_bytes(value: bytes) -> str:
    return "sha256:" + hashlib.sha256(value).hexdigest()


def _sha256_data(value: object) -> str:
    encoded = json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return _sha256_bytes(encoded)


class ReleaseTrustedEvidenceBundleTests(unittest.TestCase):
    def test_complete_bundle_is_copied_byte_exactly_by_powershell_51(self) -> None:
        powershell = shutil.which("powershell") or shutil.which("pwsh")
        if powershell is None:
            self.skipTest("PowerShell is unavailable")
        with TemporaryWorkspace() as workspace:
            fixture = self._write_bundle(workspace)
            completed = self._run_packager(powershell, fixture)
            self.assertEqual(
                0,
                completed.returncode,
                msg=f"PowerShell bundle packaging failed:\n{completed.stdout}\n{completed.stderr}",
            )
            result = json.loads(fixture["result"].read_text(encoding="utf-8"))
            self.assertEqual(1, result["projectCount"])
            self.assertEqual(1, result["assertionCount"])
            self.assertEqual(12, result["artifactCount"])
            self.assertEqual(14, result["copiedArtifactCount"])
            self.assertEqual(12, len(result["artifacts"]))
            self.assertEqual(
                {
                    "request",
                    "child_result",
                    "generated_build_props",
                    "parent_evaluation_build_props",
                    "child_evaluation_build_props",
                    "parent_validation_build_props",
                    "stdout",
                    "stderr",
                    "test_dll",
                    "trx",
                    "implementation_dll",
                    "record",
                },
                {item["kind"] for item in result["artifacts"]},
            )
            destination = fixture["trusted_root"] / SESSION_ID
            for relative in fixture["artifact_paths"]:
                self.assertEqual(
                    (fixture["session"] / relative).read_bytes(),
                    (destination / "artifacts" / relative).read_bytes(),
                    relative,
                )
            self.assertEqual(
                (fixture["session"] / "a.json").read_bytes(),
                (destination / "authority-receipt.json").read_bytes(),
            )
            self.assertEqual(
                (fixture["session"] / "i.json").read_bytes(),
                (destination / "artifact-index.json").read_bytes(),
            )
            self.assertEqual(
                14,
                len([path for path in destination.rglob("*") if path.is_file()]),
            )

    def test_report_arrays_must_be_real_json_arrays(self) -> None:
        powershell = shutil.which("powershell") or shutil.which("pwsh")
        if powershell is None:
            self.skipTest("PowerShell is unavailable")
        for property_name in (
            "result_artifact_sha256s",
            "result_artifacts",
            "target_frameworks",
        ):
            with self.subTest(property_name=property_name), TemporaryWorkspace() as workspace:
                fixture = self._write_bundle(workspace)
                control = json.loads(fixture["control"].read_text(encoding="utf-8"))
                control["evidence_execution"][property_name] = control[
                    "evidence_execution"
                ][property_name][0]
                fixture["control"].write_text(
                    json.dumps(control, sort_keys=True),
                    encoding="utf-8",
                    newline="\n",
                )
                completed = self._run_packager(powershell, fixture)
                self.assertNotEqual(0, completed.returncode)
                self.assertIn("singleton JSON arrays", completed.stderr)

    def test_adversarial_bundles_fail_closed(self) -> None:
        powershell = shutil.which("powershell") or shutil.which("pwsh")
        if powershell is None:
            self.skipTest("PowerShell is unavailable")
        cases = (
            "bare-trace-hash",
            "bare-index-entry-hash",
            "tampered-dll",
            "path-traversal",
            "path-absolute",
            "path-drive",
            "path-unc",
            "path-backslash",
            "path-ads",
            "path-reserved",
            "path-case-collision",
            "duplicate-path",
            "missing-artifact",
            "extra-artifact",
            "trace-count-mismatch",
            "trace-count-string",
            "receipt-extra-property",
            "index-missing-property",
            "report-assertion-id-mismatch",
            "child-input-mismatch",
            "child-package-lock-mismatch",
            "child-arguments-mismatch",
            "child-graph-mismatch",
        )
        for case in cases:
            with self.subTest(case=case), TemporaryWorkspace() as workspace:
                fixture = self._write_bundle(workspace)
                self._mutate_bundle(fixture, case)
                completed = self._run_packager(powershell, fixture)
                self.assertNotEqual(
                    0,
                    completed.returncode,
                    msg=f"adversarial bundle '{case}' was accepted",
                )

    def test_reproduced_release_forgeries_fail_at_independent_bindings(self) -> None:
        powershell = shutil.which("powershell") or shutil.which("pwsh")
        if powershell is None:
            self.skipTest("PowerShell is unavailable")
        cases = {
            "receipt-request-hash-forgery": "request hashes do not match actual q.json bytes",
            "receipt-child-hash-forgery": "child-result hashes do not match actual z.json bytes",
            "failed-assertion": "not exact passing canonical evidence",
            "nonzero-exit": "command/graph/exit binding is invalid",
            "forged-output": "not exact passing canonical evidence",
            "forged-evidence-results-hash": "forged EvidenceResults content hash",
            "forged-g2": "canonical request/child artifact closure",
        }
        for case, error_fragment in cases.items():
            with self.subTest(case=case), TemporaryWorkspace() as workspace:
                fixture = self._write_bundle(workspace)
                self._mutate_bundle(fixture, case)
                completed = self._run_packager(powershell, fixture)
                self.assertNotEqual(
                    0,
                    completed.returncode,
                    msg=f"reproduced release forgery '{case}' was accepted",
                )
                self.assertIn(error_fragment, completed.stderr)

    def test_hardlinked_and_reparse_artifacts_fail_closed_when_supported(self) -> None:
        powershell = shutil.which("powershell") or shutil.which("pwsh")
        if powershell is None:
            self.skipTest("PowerShell is unavailable")

        with TemporaryWorkspace() as workspace:
            fixture = self._write_bundle(workspace)
            dll = (
                fixture["session"]
                / "p"
                / PROJECT_SLUG
                / "b"
                / "Product.Tests.dll"
            )
            alias = fixture["session"] / "hardlink-alias.bin"
            try:
                os.link(dll, alias)
            except OSError as exception:
                self.skipTest(f"hardlinks are unavailable: {exception}")
            completed = self._run_packager(powershell, fixture)
            self.assertNotEqual(0, completed.returncode)
            self.assertIn("hardlinked", completed.stderr)

        with TemporaryWorkspace() as workspace:
            fixture = self._write_bundle(workspace)
            record = (
                fixture["session"] / "p" / PROJECT_SLUG / "r" / "case.json"
            )
            outside = workspace.path / "outside-record.json"
            outside.write_bytes(record.read_bytes())
            record.unlink()
            try:
                os.symlink(outside, record)
            except OSError as exception:
                record.write_bytes(outside.read_bytes())
                self.skipTest(f"file symlinks are unavailable: {exception}")
            completed = self._run_packager(powershell, fixture)
            self.assertNotEqual(0, completed.returncode)
            self.assertIn("reparse point", completed.stderr)

    def _write_bundle(self, workspace: TemporaryWorkspace) -> dict[str, object]:
        repository = workspace.path / "repo"
        session = repository / "temp" / "u" / SESSION_ID
        release = workspace.path / "release"
        trusted_root = release / "trusted-evidence"
        (session / "s").mkdir(parents=True)
        release.mkdir(parents=True)

        canonical_receipt = {
            "assertion": "Exact 한글 </script> \u2028 behavior matches the pinned contract.",
            "claims_active_load": False,
            "exercised_load": "not_applicable",
            "expected_output_sha256": HASH_C,
            "id": ASSERTION_ID,
            "outcome": "passed",
            "skipped": False,
            "structural_only": False,
            "test_path": "tests/Product.Tests/ParityTests.cs",
            "test_source_sha256": HASH_A,
            "test_symbol": "Product.Tests.ParityTests.Matches",
            "verification_kind": "unit_behavior",
        }
        evidence_entries = [
            {
                "implementation": {
                    "path": "src/Product/Service.cs",
                    "source_sha256": HASH_B,
                    "symbol": "Product.Service.Run",
                },
                "path": "upstream/service.py",
                "receipts": [canonical_receipt],
                "symbol": "Service.run",
                "upstream_symbol_hash": HASH_A,
            }
        ]
        evidence_content = {
            "entries": evidence_entries,
            "inventory_sha256": HASH_A,
            "upstream_commit": UPSTREAM_COMMIT,
        }
        symbol_evidence_sha256 = _sha256_data(evidence_content)
        symbol_evidence = {
            "content_sha256": symbol_evidence_sha256,
            **evidence_content,
            "schema": "goniegonie.upstream-symbol-evidence.v1",
            "summary": {
                "entry_count": 1,
                "passed_receipt_count": 1,
                "receipt_count": 1,
                "skipped_receipt_count": 0,
                "structural_only_receipt_count": 0,
                "zero_load_active_claim_count": 0,
            },
        }
        repository_evidence_path = repository / "upstream" / "symbol-evidence.json"
        source_evidence_path = session / "s" / "upstream" / "symbol-evidence.json"
        self._write_json(repository_evidence_path, symbol_evidence)
        source_evidence_path.parent.mkdir(parents=True, exist_ok=True)
        source_evidence_path.write_bytes(repository_evidence_path.read_bytes())
        source_evidence_file_sha256 = _sha256_bytes(
            repository_evidence_path.read_bytes()
        )
        source_files = [
            {
                "path": "upstream/symbol-evidence.json",
                "sha256": source_evidence_file_sha256,
            }
        ]
        source_tree_sha256 = _sha256_data({"files": source_files})
        expected_arguments = [
            "C:/pinned/dotnet/dotnet.exe",
            "test",
            "--framework",
            "net8.0-windows",
        ]

        artifact_bytes = {
            f"c/{PROJECT_SLUG}/d.props": b"<Project>generated</Project>\n",
            f"g0/{PROJECT_SLUG}/d.props": b"<Project>parent-eval</Project>\n",
            f"g1/{PROJECT_SLUG}/d.props": b"<Project>child-eval</Project>\n",
            f"g2/{PROJECT_SLUG}/d.props": b"<Project>parent-validation</Project>\n",
            f"p/{PROJECT_SLUG}/stdout.bin": b"fresh stdout\r\n",
            f"p/{PROJECT_SLUG}/stderr.bin": b"",
            f"p/{PROJECT_SLUG}/b/Product.Tests.dll": b"fresh test dll",
            f"p/{PROJECT_SLUG}/b/Product.dll": b"fresh implementation dll",
            f"p/{PROJECT_SLUG}/t/results.trx": b"<TestRun />\n",
            f"p/{PROJECT_SLUG}/r/case.json": b'{"outcome":"exact"}\n',
        }
        for relative, content in artifact_bytes.items():
            path = session / Path(relative)
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)

        descriptors = {
            relative: self._descriptor(session, relative)
            for relative in artifact_bytes
        }
        request = {
            "assertion_count": 1,
            "dotnet": {
                "path": "C:/pinned/dotnet/dotnet.exe",
                "sdk_manifest": {
                    "file_count": 1,
                    "files": [
                        {"bytes": 1, "path": "MSBuild.dll", "sha256": HASH_A}
                    ],
                    "root": "C:/pinned/dotnet/sdk/8.0.424",
                    "schema": "goniegonie.trusted-dotnet-sdk-manifest.v1",
                    "sha256": HASH_B,
                },
                "sdk_root": "C:/pinned/dotnet/sdk/8.0.424",
                "sdk_version": "8.0.424",
                "sha256": HASH_C,
            },
            "evidence_binding": {
                "collector_path": "tools/upstream-tracker/goniegonie_upstream_tracker/trusted_collector.py",
                "collector_source_sha256": HASH_A,
                "collector_symbol": "collect_trusted_evidence",
                "inventory_sha256": HASH_A,
                "matrix_sha256": HASH_B,
                "symbol_evidence_sha256": symbol_evidence_sha256,
                "upstream_commit": UPSTREAM_COMMIT,
            },
            "git": {"path": "C:/Git/bin/git.exe", "sha256": HASH_B},
            "inputs": source_files,
            "nonce": NONCE,
            "package_locks": [],
            "project_count": 1,
            "projects": [
                {
                    "arguments": expected_arguments,
                    "assembly_name": "Product.Tests",
                    "assertions": [
                        {
                            "exercised_load": "not_applicable",
                            "id": ASSERTION_ID,
                            "test_path": "tests/Product.Tests/ParityTests.cs",
                            "test_source_sha256": HASH_A,
                            "test_symbol": "Product.Tests.ParityTests.Matches",
                        }
                    ],
                    "build_props": descriptors[f"c/{PROJECT_SLUG}/d.props"],
                    "evaluated_graph": {},
                    "implementation_assemblies": ["Product"],
                    "path": PROJECT_PATH,
                    "planning_build_props": descriptors[
                        f"g0/{PROJECT_SLUG}/d.props"
                    ],
                    "slug": PROJECT_SLUG,
                }
            ],
            "repository_head": REPOSITORY_HEAD,
            "repository_root": repository.as_posix(),
            "required_assertion_ids": [ASSERTION_ID],
            "schema": "goniegonie.trusted-evidence-request.v1",
            "session_directory": session.as_posix(),
            "session_id": SESSION_ID,
            "source": {
                "file_count": 1,
                "files": source_files,
                "root": (session / "s").as_posix(),
                "sha256": source_tree_sha256,
            },
            "target_framework": "net8.0-windows",
        }
        self._write_json(session / "q.json", request)
        request_descriptor = self._descriptor(session, "q.json")
        executed_assertion = {
            "assertion_id": ASSERTION_ID,
            "exercised_load": "not_applicable",
            "outcome": "passed",
            "output_sha256": HASH_C,
            "skipped": False,
            "structural_only": False,
            "test_path": "tests/Product.Tests/ParityTests.cs",
            "test_source_sha256": HASH_A,
            "test_symbol": "Product.Tests.ParityTests.Matches",
        }
        child = {
            "artifact_count": 12,
            "assertion_count": 1,
            "assertions": [executed_assertion],
            "git_executable_sha256": HASH_B,
            "inputs": source_files,
            "nonce": NONCE,
            "package_locks": [],
            "project_count": 1,
            "projects": [
                {
                    "arguments": expected_arguments,
                    "assertions": [executed_assertion],
                    "evaluated_graph": {},
                    "evaluation_build_props": descriptors[
                        f"g1/{PROJECT_SLUG}/d.props"
                    ],
                    "exit_code": 0,
                    "implementation_dlls": [
                        descriptors[f"p/{PROJECT_SLUG}/b/Product.dll"]
                    ],
                    "path": PROJECT_PATH,
                    "parent_validation_build_props": descriptors[
                        f"g2/{PROJECT_SLUG}/d.props"
                    ],
                    "records": [descriptors[f"p/{PROJECT_SLUG}/r/case.json"]],
                    "stderr": descriptors[f"p/{PROJECT_SLUG}/stderr.bin"],
                    "stdout": descriptors[f"p/{PROJECT_SLUG}/stdout.bin"],
                    "test_dll": descriptors[
                        f"p/{PROJECT_SLUG}/b/Product.Tests.dll"
                    ],
                    "trx": descriptors[f"p/{PROJECT_SLUG}/t/results.trx"],
                }
            ],
            "repository_head": REPOSITORY_HEAD,
            "request_sha256": request_descriptor["sha256"],
            "schema": "goniegonie.trusted-evidence-child-result.v1",
            "session_id": SESSION_ID,
            "source_tree_sha256": source_tree_sha256,
            "target_framework": "net8.0-windows",
            "toolchain_manifest_sha256": HASH_B,
        }
        self._write_json(session / "z.json", child)
        child_descriptor = self._descriptor(session, "z.json")

        def indexed(kind: str, descriptor: dict, *, project: bool = True) -> dict:
            value = {**descriptor, "kind": kind}
            if project:
                value["project_path"] = PROJECT_PATH
            return value

        entries = [
            indexed("request", request_descriptor, project=False),
            indexed("child_result", child_descriptor, project=False),
            indexed(
                "generated_build_props",
                descriptors[f"c/{PROJECT_SLUG}/d.props"],
            ),
            indexed(
                "parent_evaluation_build_props",
                descriptors[f"g0/{PROJECT_SLUG}/d.props"],
            ),
            indexed(
                "child_evaluation_build_props",
                descriptors[f"g1/{PROJECT_SLUG}/d.props"],
            ),
            indexed(
                "parent_validation_build_props",
                descriptors[f"g2/{PROJECT_SLUG}/d.props"],
            ),
            indexed("stdout", descriptors[f"p/{PROJECT_SLUG}/stdout.bin"]),
            indexed("stderr", descriptors[f"p/{PROJECT_SLUG}/stderr.bin"]),
            indexed(
                "test_dll", descriptors[f"p/{PROJECT_SLUG}/b/Product.Tests.dll"]
            ),
            indexed(
                "implementation_dll",
                descriptors[f"p/{PROJECT_SLUG}/b/Product.dll"],
            ),
            indexed("trx", descriptors[f"p/{PROJECT_SLUG}/t/results.trx"]),
            indexed("record", descriptors[f"p/{PROJECT_SLUG}/r/case.json"]),
        ]
        entries.sort(key=lambda item: (item["path"], item["kind"]))
        index = {
            "artifact_count": 12,
            "artifacts": entries,
            "assertion_count": 1,
            "child_result_sha256": child_descriptor["sha256"],
            "dotnet_executable_sha256": HASH_C,
            "git_executable_sha256": HASH_B,
            "project_count": 1,
            "repository_head": REPOSITORY_HEAD,
            "request_sha256": request_descriptor["sha256"],
            "schema": "goniegonie.trusted-evidence-artifact-index.v1",
            "session_id": SESSION_ID,
            "source_tree_sha256": source_tree_sha256,
            "target_framework": "net8.0-windows",
            "toolchain_manifest_sha256": HASH_B,
        }
        self._write_json(session / "i.json", index)
        index_descriptor = self._descriptor(session, "i.json")
        receipt = {
            "artifact_count": 12,
            "artifact_index_path": "i.json",
            "artifact_index_sha256": index_descriptor["sha256"],
            "assertion_count": 1,
            "child_result_sha256": child_descriptor["sha256"],
            "collector_source_sha256": HASH_A,
            "dotnet_executable_sha256": HASH_C,
            "evidence_results_sha256": _sha256_data(
                {
                    "assertions": [executed_assertion],
                    "collector": {
                        "path": "tools/upstream-tracker/goniegonie_upstream_tracker/trusted_collector.py",
                        "source_sha256": HASH_A,
                        "symbol": "collect_trusted_evidence",
                    },
                    "inventory_sha256": HASH_A,
                    "symbol_evidence_sha256": symbol_evidence_sha256,
                    "target_framework": "net8.0-windows",
                    "upstream_commit": UPSTREAM_COMMIT,
                }
            ),
            "git_executable_sha256": HASH_B,
            "inventory_sha256": HASH_A,
            "matrix_sha256": HASH_B,
            "project_count": 1,
            "repository_head": REPOSITORY_HEAD,
            "request_sha256": request_descriptor["sha256"],
            "schema": "goniegonie.trusted-evidence-authority-receipt.v1",
            "session_id": SESSION_ID,
            "source_tree_sha256": source_tree_sha256,
            "symbol_evidence_sha256": symbol_evidence_sha256,
            "target_framework": "net8.0-windows",
            "toolchain_manifest_sha256": HASH_B,
            "upstream_commit": UPSTREAM_COMMIT,
        }
        self._write_json(session / "a.json", receipt)
        receipt_descriptor = self._descriptor(session, "a.json")
        trace = {
            "artifact_count": 12,
            "artifact_index_path": f"temp/u/{SESSION_ID}/i.json",
            "artifact_index_sha256": index_descriptor["sha256"],
            "assertion_count": 1,
            "authority_receipt_path": f"temp/u/{SESSION_ID}/a.json",
            "authority_receipt_sha256": receipt_descriptor["sha256"],
            "project_count": 1,
            "session_id": SESSION_ID,
        }
        control = {
            "evidence_execution": {
                "result_artifact_sha256s": [receipt_descriptor["sha256"]],
                "result_artifacts": [trace],
                "target_frameworks": ["net8.0-windows"],
            },
            "expected": {
                "assertion_count": 1,
                "assertion_ids": [ASSERTION_ID],
                "inventory_sha256": HASH_A,
                "matrix_sha256": HASH_B,
                "repository_head": REPOSITORY_HEAD,
                "symbol_evidence_sha256": symbol_evidence_sha256,
                "target_framework": "net8.0-windows",
                "upstream_commit": UPSTREAM_COMMIT,
            },
        }
        control_path = workspace.path / "control.json"
        self._write_json(control_path, control)
        return {
            "artifact_paths": sorted(["q.json", "z.json", *artifact_bytes]),
            "control": control_path,
            "release": release,
            "repository": repository,
            "result": workspace.path / "result.json",
            "session": session,
            "trusted_root": trusted_root,
        }

    def _mutate_bundle(self, fixture: dict[str, object], case: str) -> None:
        session = fixture["session"]
        control_path = fixture["control"]
        if case == "bare-trace-hash":
            control = json.loads(control_path.read_text(encoding="utf-8"))
            control["evidence_execution"]["result_artifacts"][0][
                "authority_receipt_sha256"
            ] = control["evidence_execution"]["result_artifacts"][0][
                "authority_receipt_sha256"
            ].removeprefix("sha256:")
            self._write_json(control_path, control)
            return
        if case == "tampered-dll":
            (session / "p" / PROJECT_SLUG / "b" / "Product.Tests.dll").write_bytes(
                b"tampered after authority"
            )
            return
        if case == "trace-count-mismatch":
            control = json.loads(control_path.read_text(encoding="utf-8"))
            control["evidence_execution"]["result_artifacts"][0][
                "artifact_count"
            ] = 13
            self._write_json(control_path, control)
            return
        if case == "trace-count-string":
            control = json.loads(control_path.read_text(encoding="utf-8"))
            control["evidence_execution"]["result_artifacts"][0][
                "artifact_count"
            ] = "12"
            self._write_json(control_path, control)
            return
        if case == "report-assertion-id-mismatch":
            control = json.loads(control_path.read_text(encoding="utf-8"))
            control["expected"]["assertion_ids"] = ["forged-assertion"]
            self._write_json(control_path, control)
            return
        if case == "forged-g2":
            validation_path = session / "g2" / PROJECT_SLUG / "d.props"
            validation_path.write_bytes(b"<Project>forged validation</Project>\n")

        def mutate_index(index: dict) -> None:
            record = next(item for item in index["artifacts"] if item["kind"] == "record")
            if case == "bare-index-entry-hash":
                record["sha256"] = record["sha256"].removeprefix("sha256:")
            elif case == "path-traversal":
                record["path"] = "../escape.json"
            elif case == "path-absolute":
                record["path"] = "/escape.json"
            elif case == "path-drive":
                record["path"] = "C:/escape.json"
            elif case == "path-unc":
                record["path"] = "//server/share/escape.json"
            elif case == "path-backslash":
                record["path"] = r"p\escape.json"
            elif case == "path-ads":
                record["path"] = f"p/{PROJECT_SLUG}/r/case.json:stream"
            elif case == "path-reserved":
                record["path"] = f"p/{PROJECT_SLUG}/r/con.txt"
            elif case in {"path-case-collision", "duplicate-path"}:
                duplicate = dict(record)
                if case == "path-case-collision":
                    duplicate["path"] = record["path"].upper()
                index["artifacts"].append(duplicate)
                index["artifact_count"] = 13
                index["artifacts"].sort(key=lambda item: (item["path"], item["kind"]))
            elif case == "missing-artifact":
                index["artifacts"] = [
                    item for item in index["artifacts"] if item["kind"] != "stdout"
                ]
                index["artifact_count"] = 11
            elif case == "extra-artifact":
                extra_path = f"p/{PROJECT_SLUG}/r/extra.json"
                extra_file = session / Path(extra_path)
                extra_file.parent.mkdir(parents=True, exist_ok=True)
                extra_file.write_bytes(b'{"extra":true}\n')
                index["artifacts"].append(
                    {
                        **self._descriptor(session, extra_path),
                        "kind": "record",
                        "project_path": PROJECT_PATH,
                    }
                )
                index["artifact_count"] = 13
                index["artifacts"].sort(key=lambda item: (item["path"], item["kind"]))
            elif case == "index-missing-property":
                index.pop("source_tree_sha256")
            elif case == "receipt-request-hash-forgery":
                index["request_sha256"] = "sha256:" + "d" * 64
            elif case == "receipt-child-hash-forgery":
                index["child_result_sha256"] = "sha256:" + "d" * 64
            elif case == "forged-g2":
                validation = next(
                    item
                    for item in index["artifacts"]
                    if item["kind"] == "parent_validation_build_props"
                )
                validation.update(
                    self._descriptor(session, f"g2/{PROJECT_SLUG}/d.props")
                )

        def mutate_child(child: dict) -> None:
            if case in {"path-case-collision", "duplicate-path", "extra-artifact"}:
                child["artifact_count"] = 13
            elif case == "missing-artifact":
                child["artifact_count"] = 11
            elif case == "failed-assertion":
                child["assertions"][0]["outcome"] = "failed"
                child["projects"][0]["assertions"][0]["outcome"] = "failed"
            elif case == "nonzero-exit":
                child["projects"][0]["exit_code"] = 1
            elif case == "forged-output":
                child["assertions"][0]["output_sha256"] = HASH_B
                child["projects"][0]["assertions"][0]["output_sha256"] = HASH_B
            elif case == "child-input-mismatch":
                child["inputs"] = []
            elif case == "child-package-lock-mismatch":
                child["package_locks"] = [
                    {"path": "forged/packages.lock.json", "sha256": HASH_A}
                ]
            elif case == "child-arguments-mismatch":
                child["projects"][0]["arguments"].append("--forged")
            elif case == "child-graph-mismatch":
                child["projects"][0]["evaluated_graph"] = {"forged": True}

        def mutate_receipt(receipt: dict) -> None:
            if case == "receipt-extra-property":
                receipt["forged"] = True
            elif case == "receipt-request-hash-forgery":
                receipt["request_sha256"] = "sha256:" + "d" * 64
            elif case == "receipt-child-hash-forgery":
                receipt["child_result_sha256"] = "sha256:" + "d" * 64
            elif case == "forged-evidence-results-hash":
                receipt["evidence_results_sha256"] = "sha256:" + "d" * 64
            elif case == "forged-output":
                request = json.loads((session / "q.json").read_text(encoding="utf-8"))
                child = json.loads((session / "z.json").read_text(encoding="utf-8"))
                binding = request["evidence_binding"]
                receipt["evidence_results_sha256"] = _sha256_data(
                    {
                        "assertions": child["assertions"],
                        "collector": {
                            "path": binding["collector_path"],
                            "source_sha256": binding["collector_source_sha256"],
                            "symbol": binding["collector_symbol"],
                        },
                        "inventory_sha256": binding["inventory_sha256"],
                        "symbol_evidence_sha256": binding[
                            "symbol_evidence_sha256"
                        ],
                        "target_framework": request["target_framework"],
                        "upstream_commit": binding["upstream_commit"],
                    }
                )

        self._resign_bundle(
            fixture,
            mutate_child=mutate_child,
            mutate_index=mutate_index,
            mutate_receipt=mutate_receipt,
        )

    def _resign_bundle(
        self,
        fixture: dict[str, object],
        *,
        mutate_child=None,
        mutate_index=None,
        mutate_receipt=None,
    ) -> None:
        session = fixture["session"]
        child_path = session / "z.json"
        child = json.loads(child_path.read_text(encoding="utf-8"))
        if mutate_child is not None:
            mutate_child(child)
        self._write_json(child_path, child)
        child_descriptor = self._descriptor(session, "z.json")

        index_path = session / "i.json"
        index = json.loads(index_path.read_text(encoding="utf-8"))
        index["child_result_sha256"] = child_descriptor["sha256"]
        child_entry = next(
            item for item in index["artifacts"] if item["kind"] == "child_result"
        )
        child_entry.update(child_descriptor)
        if mutate_index is not None:
            mutate_index(index)
        self._write_json(index_path, index)
        index_descriptor = self._descriptor(session, "i.json")

        receipt_path = session / "a.json"
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        receipt["artifact_index_sha256"] = index_descriptor["sha256"]
        receipt["child_result_sha256"] = child_descriptor["sha256"]
        if "artifact_count" in index:
            receipt["artifact_count"] = index["artifact_count"]
        if mutate_receipt is not None:
            mutate_receipt(receipt)
        self._write_json(receipt_path, receipt)
        receipt_descriptor = self._descriptor(session, "a.json")

        control_path = fixture["control"]
        control = json.loads(control_path.read_text(encoding="utf-8"))
        trace = control["evidence_execution"]["result_artifacts"][0]
        trace["artifact_index_sha256"] = index_descriptor["sha256"]
        trace["authority_receipt_sha256"] = receipt_descriptor["sha256"]
        trace["artifact_count"] = receipt["artifact_count"]
        control["evidence_execution"]["result_artifact_sha256s"] = [
            receipt_descriptor["sha256"]
        ]
        self._write_json(control_path, control)

    def _run_packager(self, powershell: str, fixture: dict[str, object]) -> subprocess.CompletedProcess[str]:
        release_script = self._ps(REPOSITORY_ROOT / "scripts" / "release.ps1")
        control = self._ps(fixture["control"])
        repository = self._ps(fixture["repository"])
        release = self._ps(fixture["release"])
        trusted_root = self._ps(fixture["trusted_root"])
        result = self._ps(fixture["result"])
        probe = rf"""
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    '{release_script}', [ref] $tokens, [ref] $errors)
if ($errors.Count -ne 0) {{ throw $errors[0] }}
$baseNames = @(
    'Test-PathWithin',
    'Assert-NoReparseAncestorChain',
    'Get-RelativeUnixPath',
    'Get-BytesSha256',
    'Assert-NoDuplicateJsonObjectKeys',
    'Write-BytesAtomicallyExclusive',
    'Get-JsonInteger')
$definitions = $ast.FindAll({{
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        ($baseNames -contains $node.Name -or $node.Name -like '*TrustedEvidence*')
}}, $true)
foreach ($definition in $definitions) {{
    . ([scriptblock]::Create($definition.Extent.Text))
}}
$control = Get-Content -LiteralPath '{control}' -Raw | ConvertFrom-Json
$reportSession = Get-TrustedEvidenceReportSession `
    -EvidenceExecution $control.evidence_execution
$packaged = Copy-TrustedEvidenceSession `
    -RepositoryRoot '{repository}' `
    -ReleaseRoot '{release}' `
    -TrustedEvidenceReleaseRoot '{trusted_root}' `
    -Trace $reportSession.trace `
    -ExpectedAuthorityReceiptSha256 $reportSession.authorityReceiptSha256 `
    -ExpectedRepositoryHead ([string] $control.expected.repository_head) `
    -ExpectedUpstreamCommit ([string] $control.expected.upstream_commit) `
    -ExpectedInventorySha256 ([string] $control.expected.inventory_sha256) `
    -ExpectedMatrixSha256 ([string] $control.expected.matrix_sha256) `
    -ExpectedSymbolEvidenceSha256 ([string] $control.expected.symbol_evidence_sha256) `
    -ExpectedTargetFramework ([string] $control.expected.target_framework) `
    -ExpectedAssertionCount ([long] $control.expected.assertion_count) `
    -ExpectedAssertionIds $control.expected.assertion_ids
[System.IO.File]::WriteAllText(
    '{result}',
    ($packaged | ConvertTo-Json -Depth 32),
    [System.Text.UTF8Encoding]::new($false))
"""
        return subprocess.run(
            [powershell, "-NoProfile", "-NonInteractive", "-Command", probe],
            capture_output=True,
            text=True,
            check=False,
        )

    @staticmethod
    def _descriptor(root: Path, relative: str) -> dict[str, object]:
        content = (root / Path(relative)).read_bytes()
        return {
            "bytes": len(content),
            "path": relative,
            "sha256": _sha256_bytes(content),
        }

    @staticmethod
    def _write_json(path: Path, value: object) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            json.dumps(value, ensure_ascii=False, sort_keys=True),
            encoding="utf-8",
            newline="\n",
        )

    @staticmethod
    def _ps(value: object) -> str:
        return str(value).replace("'", "''")


if __name__ == "__main__":
    unittest.main()

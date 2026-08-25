from __future__ import annotations

from html import escape
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import unittest

from support import REPOSITORY_ROOT, TemporaryWorkspace

from goniegonie_upstream_tracker.evidence import (
    EvidenceReceipt,
    EvidenceResults,
    ExecutedAssertion,
    SymbolEvidence,
    SymbolEvidenceRegistry,
    evaluate_evidence_execution,
)
import goniegonie_upstream_tracker.trusted_collector as trusted_collector
from goniegonie_upstream_tracker.trusted_collector import (
    RECORD_SCHEMA,
    REQUEST_SCHEMA,
    TrustedCollectorError,
    TrustedEvidenceResults,
    _build_session_artifact_index,
    _captured_session_artifact,
    _dotnet_restore_command,
    _exact_repository_snapshot,
    _dotnet_test_command,
    _evaluate_project_graph,
    _hmac_sha256,
    _isolated_dotnet_environment,
    _json_loads,
    _materialize_source_tree,
    _normalize_evaluated_graph,
    _parse_project_artifacts,
    _run_test_project,
    _sha256_bytes,
    _sha256_data,
    _sdk_toolchain_manifest,
    _validate_child_result_artifacts,
    _validate_request,
    _validate_session_artifact,
    _verify_canonical_evidence_binding,
    _verify_materialized_source,
    _verify_sdk_toolchain_manifest,
    _relative_path,
    _resolve_git,
    _project_assembly_name,
    _sanitized_environment,
    _write_project_build_props,
    authority_receipt_sha256,
    is_authoritative_evidence_results,
)


HASH_A = "sha256:" + "a" * 64
HASH_B = "sha256:" + "b" * 64
HASH_C = "sha256:" + "c" * 64
COMMIT = "1" * 40
SESSION_ID = "2" * 32
NONCE = "3" * 64
TEST_PATH = "tests/Product.Tests/ParityTests.cs"
TEST_SYMBOL = "Product.Tests.ParityTests.Matches"


class TrustedCollectorTests(unittest.TestCase):
    def test_artifact_index_uses_parent_validated_g2_descriptor_from_z(self) -> None:
        def artifact(path: str, value: str) -> dict[str, object]:
            return {"bytes": 1, "path": path, "sha256": value}

        project_path = "tests/Product.Tests/Product.Tests.csproj"
        request = {
            "assertion_count": 1,
            "dotnet": {"sha256": HASH_A, "sdk_manifest": {"sha256": HASH_B}},
            "git": {"sha256": HASH_C},
            "project_count": 1,
            "projects": [
                {
                    "build_props": artifact("c/slug/d.props", HASH_A),
                    "path": project_path,
                    "planning_build_props": artifact("g0/slug/d.props", HASH_B),
                }
            ],
            "repository_head": COMMIT,
            "session_id": SESSION_ID,
            "source": {"sha256": HASH_C},
            "target_framework": "net8.0-windows",
        }
        child = {
            "artifact_count": 14,
            "assertion_count": 1,
            "assertions": [{"assertion_id": "service-parity"}],
            "project_count": 1,
            "projects": [
                {
                    "evaluation_build_props": artifact("g1/slug/d.props", HASH_A),
                    "implementation_dlls": [artifact("p/slug/b/Product.dll", HASH_A)],
                    "parent_validation_build_props": artifact(
                        "g2/slug/d.props", HASH_C
                    ),
                    "path": project_path,
                    "records": [artifact("p/slug/r/case.json", HASH_B)],
                    "restore_stderr": artifact(
                        "p/slug/restore.stderr.bin", HASH_A
                    ),
                    "restore_stdout": artifact(
                        "p/slug/restore.stdout.bin", HASH_B
                    ),
                    "stderr": artifact("p/slug/stderr.bin", HASH_A),
                    "stdout": artifact("p/slug/stdout.bin", HASH_B),
                    "test_dll": artifact("p/slug/b/Product.Tests.dll", HASH_C),
                    "trx": artifact("p/slug/t/results.trx", HASH_A),
                }
            ],
        }
        index = _build_session_artifact_index(
            request,
            child,
            artifact("q.json", HASH_A),
            artifact("z.json", HASH_B),
        )
        validation = next(
            item
            for item in index["artifacts"]
            if item["kind"] == "parent_validation_build_props"
        )
        self.assertEqual(child["projects"][0]["parent_validation_build_props"], {
            key: validation[key] for key in ("bytes", "path", "sha256")
        })

    def test_sdk_manifest_binds_the_complete_toolchain_file_set(self) -> None:
        with TemporaryWorkspace() as workspace:
            sdk = workspace.path / "sdk" / "8.0.424"
            (sdk / "Sdks" / "Microsoft.NET.Sdk" / "targets").mkdir(parents=True)
            (sdk / "MSBuild.dll").write_bytes(b"msbuild")
            target = sdk / "Sdks" / "Microsoft.NET.Sdk" / "targets" / "Default.targets"
            target.write_bytes(b"targets")
            manifest = _sdk_toolchain_manifest(sdk)
            self.assertEqual(2, manifest["file_count"])
            self.assertEqual(
                ["MSBuild.dll", "Sdks/Microsoft.NET.Sdk/targets/Default.targets"],
                [item["path"] for item in manifest["files"]],
            )
            _verify_sdk_toolchain_manifest(manifest)
            target.write_bytes(b"forged")
            with self.assertRaisesRegex(TrustedCollectorError, "toolchain changed"):
                _verify_sdk_toolchain_manifest(manifest)

    def test_isolated_dotnet_environment_precreates_windows_special_folders(self) -> None:
        with TemporaryWorkspace() as workspace:
            environment = _isolated_dotnet_environment(workspace.path / "environment")
            profile = Path(environment["USERPROFILE"])
            if os.name == "nt":
                self.assertEqual(
                    profile / "AppData" / "Roaming",
                    Path(environment["APPDATA"]),
                )
                self.assertEqual(
                    profile / "AppData" / "Local",
                    Path(environment["LOCALAPPDATA"]),
                )
            self.assertTrue(Path(environment["APPDATA"]).is_dir())
            self.assertTrue(Path(environment["LOCALAPPDATA"]).is_dir())

    def test_recorder_is_noop_outside_collection_and_requires_paired_environment(self) -> None:
        dotnet = REPOSITORY_ROOT / ".tools" / "dotnet" / "dotnet.exe"
        if not dotnet.is_file():
            self.skipTest("repository-pinned dotnet is unavailable")
        recorder = REPOSITORY_ROOT / "tools" / "upstream-tracker" / "csharp" / "TrustedEvidenceRecorder.cs"
        with TemporaryWorkspace() as workspace:
            source = workspace.path / "recorder"
            source.mkdir()
            (source / "RecorderProbe.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
                '<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>'
                '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
                '</PropertyGroup><ItemGroup>'
                '<Compile Include="Program.cs" />'
                f'<Compile Include="{escape(str(recorder))}" Link="TrustedEvidenceRecorder.cs" />'
                '</ItemGroup></Project>\n',
                encoding="utf-8",
                newline="\n",
            )
            (source / "Program.cs").write_text(
                """using GonieGonie.UpstreamTracker;
try
{
    if (args[0] == "invalid")
        TrustedEvidenceRecorder.Record("", "", "bad", null);
    else
        TrustedEvidenceRecorder.Record("recorder-probe", "Probe.Case", "zero", new { value = 1 });
    return 0;
}
catch (InvalidOperationException)
{
    return 23;
}
""",
                encoding="utf-8",
                newline="\n",
            )
            environment = _isolated_dotnet_environment(
                workspace.path / "environment", dotnet
            )
            completed = subprocess.run(
                [
                    str(dotnet),
                    "build",
                    "/noAutoResponse",
                    "RecorderProbe.csproj",
                    "--disable-build-servers",
                    "-p:ImportDirectoryBuildProps=false",
                    "-p:ImportDirectoryBuildTargets=false",
                    "-p:ImportDirectoryPackagesProps=false",
                    "-v:q",
                ],
                cwd=source,
                env=environment,
                capture_output=True,
                check=False,
            )
            self.assertEqual(
                0,
                completed.returncode,
                msg=(completed.stdout + completed.stderr).decode("utf-8", errors="replace"),
            )
            probe = source / "bin" / "Debug" / "net8.0" / "RecorderProbe.dll"
            self.assertTrue(probe.is_file(), msg=f"missing recorder probe: {probe}")
            base_environment = _isolated_dotnet_environment(
                workspace.path / "runs" / "base", dotnet
            )
            base_environment.pop("GONIEGONIE_EVIDENCE_RECORDS_DIRECTORY", None)
            base_environment.pop("GONIEGONIE_EVIDENCE_SESSION_NONCE", None)
            no_op = subprocess.run(
                [str(dotnet), str(probe), "invalid"],
                cwd=source,
                env=base_environment,
                capture_output=True,
                check=False,
            )
            self.assertEqual(
                0,
                no_op.returncode,
                msg=(no_op.stdout + no_op.stderr).decode("utf-8", errors="replace"),
            )

            one_environment = dict(base_environment)
            one_environment["GONIEGONIE_EVIDENCE_RECORDS_DIRECTORY"] = str(
                workspace.path / "records-one"
            )
            one = subprocess.run(
                [str(dotnet), str(probe), "valid"],
                cwd=source,
                env=one_environment,
                capture_output=True,
                check=False,
            )
            self.assertEqual(23, one.returncode)

            records = workspace.path / "records-both"
            both_environment = dict(base_environment)
            both_environment.update(
                {
                    "GONIEGONIE_EVIDENCE_RECORDS_DIRECTORY": str(records),
                    "GONIEGONIE_EVIDENCE_SESSION_NONCE": NONCE,
                }
            )
            both = subprocess.run(
                [str(dotnet), str(probe), "valid"],
                cwd=source,
                env=both_environment,
                capture_output=True,
                check=False,
            )
            self.assertEqual(0, both.returncode)
            record_files = list(records.glob("*.json"))
            self.assertEqual(1, len(record_files))
            self.assertEqual(RECORD_SCHEMA, json.loads(record_files[0].read_text())["schema"])

    def test_empty_authority_request_fails_before_creating_a_session(self) -> None:
        with TemporaryWorkspace() as workspace:
            with self.assertRaisesRegex(TrustedCollectorError, "at least one assertion"):
                trusted_collector.collect_trusted_evidence(
                    workspace.path,
                    None,
                    None,
                    (),
                )
            self.assertFalse((workspace.path / "temp").exists())

    def test_public_authority_route_ignores_module_global_callable_monkeypatches(self) -> None:
        _, registry = self._base_results_and_registry()
        original_collect = trusted_collector.collect_trusted_evidence
        called: list[str] = []

        def forged(*args, **kwargs):
            del args, kwargs
            called.append("forged")
            return {}

        originals = {
            name: value
            for name, value in vars(trusted_collector).items()
            if callable(value)
        }
        dependency_names = (
            "ElementTree",
            "Path",
            "base64",
            "hashlib",
            "hmac",
            "json",
            "os",
            "re",
            "secrets",
            "shutil",
            "stat",
            "subprocess",
            "sys",
            "uuid",
            "weakref",
        )
        dependencies = {
            name: getattr(trusted_collector, name) for name in dependency_names
        }
        try:
            for name in originals:
                setattr(trusted_collector, name, forged)
            for name in dependency_names:
                setattr(trusted_collector, name, forged)
            with TemporaryWorkspace() as workspace:
                with self.assertRaises(Exception) as failure:
                    original_collect(
                        workspace.path,
                        None,
                        registry,
                        ("service-parity",),
                        timeout_seconds=5,
                    )
                self.assertNotIn("forged", str(failure.exception).casefold())
        finally:
            for name, value in originals.items():
                setattr(trusted_collector, name, value)
            for name, value in dependencies.items():
                setattr(trusted_collector, name, value)
        self.assertEqual([], called)

    def test_windows_fresh_long_name_project_reference_graph_is_evaluated_and_isolated(self) -> None:
        dotnet = REPOSITORY_ROOT / ".tools" / "dotnet" / "dotnet.exe"
        sdk_root = REPOSITORY_ROOT / ".tools" / "dotnet" / "sdk" / "8.0.424"
        if not dotnet.is_file() or not sdk_root.is_dir():
            self.skipTest("repository-pinned .NET SDK 8.0.424 is unavailable")
        migration_marker = REPOSITORY_ROOT / "NuGet" / "Migrations" / "1"
        marker_before = (
            None
            if not migration_marker.exists()
            else (migration_marker.stat().st_size, migration_marker.stat().st_mtime_ns)
        )
        with TemporaryWorkspace() as workspace:
            help_environment = _isolated_dotnet_environment(
                workspace.path / "dotnet-help", dotnet
            )
            help_result = subprocess.run(
                [str(dotnet), "test", "/noAutoResponse", "--help"],
                cwd=workspace.path,
                env=help_environment,
                capture_output=True,
                check=False,
            )
            self.assertEqual(
                0,
                help_result.returncode,
                msg=(help_result.stdout + help_result.stderr).decode(
                    "utf-8", errors="replace"
                ),
            )
            session = workspace.path / "q"
            source = session / "s"
            library_dir = source / "s" / "ThisIsAnIntentionallyLongLibraryProjectNameForPathRegression"
            alternate_dir = source / "s" / "AlternateTargetOnlyProject"
            test_dir = source / "t" / "ThisIsAnIntentionallyLongTestProjectNameForPathRegression"
            library_dir.mkdir(parents=True)
            alternate_dir.mkdir(parents=True)
            test_dir.mkdir(parents=True)
            library_project = library_dir / "LongLibrary.csproj"
            alternate_project = alternate_dir / "AlternateTargetOnly.csproj"
            test_project = test_dir / "LongTest.csproj"
            library_project.write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<TargetFrameworks>net8.0;net8.0-windows</TargetFrameworks>'
                '<AssemblyName>LongLibrary</AssemblyName>'
                '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
                '</PropertyGroup><ItemGroup><Compile Include="Library.cs" />'
                '</ItemGroup><ItemGroup '
                'Condition="\'$(TargetFrameworks)\' == \'net8.0;net8.0-windows\'">'
                '<Compile Include="MultiTargetMarker.cs" />'
                '</ItemGroup></Project>\n',
                encoding="utf-8",
                newline="\n",
            )
            (library_dir / "Library.cs").write_text(
                "namespace LongNames; public sealed class Library { }\n",
                encoding="utf-8",
                newline="\n",
            )
            (library_dir / "MultiTargetMarker.cs").write_text(
                "namespace LongNames; public sealed class MultiTargetMarker { }\n",
                encoding="utf-8",
                newline="\n",
            )
            alternate_project.write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<TargetFramework>net8.0</TargetFramework>'
                '<AssemblyName>AlternateTargetOnly</AssemblyName>'
                '</PropertyGroup></Project>\n',
                encoding="utf-8",
                newline="\n",
            )
            (alternate_dir / "Alternate.cs").write_text(
                "namespace LongNames; public sealed class Alternate { }\n",
                encoding="utf-8",
                newline="\n",
            )
            relative_reference = os.path.relpath(library_project, test_dir)
            alternate_reference = os.path.relpath(alternate_project, test_dir)
            test_project.write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<TargetFrameworks>net8.0;net8.0-windows</TargetFrameworks>'
                '<AssemblyName>LongTest</AssemblyName>'
                '</PropertyGroup><ItemGroup Condition="\'$(TargetFramework)\' == \'net8.0-windows\'">'
                f'<ProjectReference Include="{escape(relative_reference)}" />'
                '</ItemGroup><ItemGroup Condition="\'$(TargetFramework)\' == \'net8.0\'">'
                f'<ProjectReference Include="{escape(alternate_reference)}" />'
                '</ItemGroup></Project>\n',
                encoding="utf-8",
                newline="\n",
            )
            (test_dir / "ConditionalCompile.cs").write_text(
                "namespace LongNames; public sealed class ConditionalCompile { }\n",
                encoding="utf-8",
                newline="\n",
            )
            (source / "NuGet.config").write_text(
                '<?xml version="1.0" encoding="utf-8"?>'
                '<configuration><packageSources><clear /></packageSources></configuration>\n',
                encoding="utf-8",
                newline="\n",
            )
            bootstrap_packages = session / "bootstrap" / "packages"
            bootstrap_environment = _isolated_dotnet_environment(
                session / "bootstrap" / "environment", dotnet
            )
            bootstrap_environment["NUGET_PACKAGES"] = str(bootstrap_packages)
            completed = subprocess.run(
                [
                    str(dotnet),
                    "restore",
                    "/noAutoResponse",
                    str(test_project),
                    "--use-lock-file",
                    "--configfile",
                    str(source / "NuGet.config"),
                    "--packages",
                    str(bootstrap_packages),
                    "--disable-build-servers",
                    "-p:NuGetAudit=false",
                ],
                cwd=source,
                env=bootstrap_environment,
                capture_output=True,
                check=False,
            )
            self.assertEqual(
                0,
                completed.returncode,
                msg=(completed.stdout + completed.stderr).decode(
                    "utf-8", errors="replace"
                ),
            )
            self.assertFalse(source.joinpath("NuGet", "Migrations", "1").exists())
            self.assertFalse(test_dir.joinpath("NuGet", "Migrations", "1").exists())
            self.assertTrue(
                Path(bootstrap_environment["LOCALAPPDATA"])
                .joinpath("NuGet", "Migrations", "1")
                .is_file()
            )
            for generated in (
                alternate_dir / "obj",
                library_dir / "obj",
                test_dir / "obj",
            ):
                if generated.exists():
                    shutil.rmtree(generated)
            files = []
            for path in sorted(item for item in source.rglob("*") if item.is_file()):
                files.append(
                    {
                        "path": path.relative_to(source).as_posix(),
                        "sha256": _sha256_bytes(path.read_bytes()),
                    }
                )
            source_tree = {
                "file_count": len(files),
                "files": files,
                "root": source.as_posix(),
                "sha256": _sha256_data({"files": files}),
            }
            project_path = test_project.relative_to(source).as_posix()
            project = {"path": project_path, "slug": "123456789abc"}
            build_props = _write_project_build_props(
                session, source, project, source_tree, stage="g9"
            )
            manifest = _sdk_toolchain_manifest(sdk_root)

            graph = _evaluate_project_graph(
                source,
                session,
                project_path,
                project["slug"],
                build_props,
                source_tree,
                "net8.0-windows",
                dotnet,
                manifest,
                "g9",
            )
            self.assertEqual(graph, _normalize_evaluated_graph(graph))
            tampered_graph = json.loads(json.dumps(graph))
            tampered_graph["projects"][0]["assembly_name"] = "Forged"
            with self.assertRaisesRegex(TrustedCollectorError, "graph hash is invalid"):
                _normalize_evaluated_graph(tampered_graph)

            self.assertEqual(2, len(graph["projects"]))
            self.assertEqual(
                {"LongLibrary", "LongTest"},
                {item["assembly_name"] for item in graph["projects"]},
            )
            self.assertNotIn(
                "AlternateTargetOnly",
                {item["assembly_name"] for item in graph["projects"]},
            )
            self.assertTrue(
                any(
                    item["path"].endswith("ConditionalCompile.cs")
                    for project_metadata in graph["projects"]
                    for item in project_metadata["compile"]
                )
            )
            self.assertTrue(
                any(
                    item["path"].endswith("MultiTargetMarker.cs")
                    for project_metadata in graph["projects"]
                    for item in project_metadata["compile"]
                )
            )
            final_props = _write_project_build_props(
                session, source, project, source_tree
            )
            build_request = {
                "dotnet": {
                    "path": dotnet.as_posix(),
                    "sdk_root": sdk_root.as_posix(),
                },
                "session_directory": session.as_posix(),
                "source": source_tree,
                "target_framework": "net8.0-windows",
            }
            build_project = {**project, "build_props": final_props}
            project_session = session / "p" / project["slug"]
            for path in (
                project_session / "b",
                project_session / "o",
                project_session / "t",
                project_session / "u",
                session / "n" / "t" / project["slug"],
            ):
                path.mkdir(parents=True, exist_ok=True)
            build_environment = _isolated_dotnet_environment(
                project_session / "e", dotnet
            )
            build_environment["NUGET_PACKAGES"] = str(
                session / "n" / "t" / project["slug"]
            )
            test_command = _dotnet_test_command(
                build_request,
                build_project,
                project_session / "b",
                project_session / "o",
                project_session / "t",
            )
            restore_command = _dotnet_restore_command(build_request, build_project)
            restore_completed = subprocess.run(
                restore_command,
                cwd=source,
                env=build_environment,
                capture_output=True,
                check=False,
            )
            self.assertEqual(
                0,
                restore_completed.returncode,
                msg=(restore_completed.stdout + restore_completed.stderr).decode(
                    "utf-8", errors="replace"
                ),
            )
            restored_projects: set[str] = set()
            restored_frameworks: dict[str, set[tuple[str, ...]]] = {}
            dgspec_paths = tuple((project_session / "o").rglob("*.nuget.dgspec.json"))
            self.assertTrue(dgspec_paths)
            for dgspec_path in dgspec_paths:
                dgspec = json.loads(dgspec_path.read_text(encoding="utf-8"))
                for raw_path, specification in dgspec["projects"].items():
                    project_name = Path(raw_path).name
                    restored_projects.add(project_name)
                    restored_frameworks.setdefault(project_name, set()).add(
                        tuple(specification["restore"]["originalTargetFrameworks"])
                    )
            self.assertIn(alternate_project.name, restored_projects)
            self.assertIn(library_project.name, restored_projects)
            self.assertIn(test_project.name, restored_projects)
            self.assertIn(
                ("net8.0", "net8.0-windows"),
                restored_frameworks[test_project.name],
            )
            property_start = test_command.index("--disable-build-servers")
            build_command = [
                str(dotnet),
                "build",
                "/noAutoResponse",
                str(test_project),
                "--configuration",
                "Release",
                "--framework",
                "net8.0-windows",
                "--no-restore",
                "--disable-build-servers",
                *test_command[property_start + 1 :],
            ]
            completed = subprocess.run(
                build_command,
                cwd=source,
                env=build_environment,
                capture_output=True,
                check=False,
            )
            self.assertEqual(
                0,
                completed.returncode,
                msg=(completed.stdout + completed.stderr).decode(
                    "utf-8", errors="replace"
                ),
            )
            self.assertNotIn("--no-build", build_command)
            self.assertNotIn("--no-build", test_command)
            self.assertIn("--no-restore", build_command)
            self.assertIn("--no-restore", test_command)
            self.assertFalse(
                any(
                    argument.startswith("-p:TargetFramework")
                    for argument in restore_command
                )
            )
            self.assertIn("/noAutoResponse", test_command)
            assembly_paths = list((project_session / "b").rglob("*.dll"))
            self.assertTrue(any(path.name == "LongLibrary.dll" for path in assembly_paths))
            self.assertTrue(any(path.name == "LongTest.dll" for path in assembly_paths))
            self.assertFalse(
                any(path.name == "AlternateTargetOnly.dll" for path in assembly_paths)
            )
            project_keys = {
                path.relative_to(project_session / "o").parts[0]
                for path in (project_session / "o").rglob("project.assets.json")
            }
            self.assertEqual(3, len(project_keys))
            created = [path for path in session.rglob("*")]
            self.assertFalse(any("$(MSBuildProjectName)" in str(path) for path in created))
            self.assertLess(max(len(str(path)) for path in created), 240)
            self.assertFalse(source.joinpath("NuGet", "Migrations", "1").exists())
            self.assertFalse(test_dir.joinpath("NuGet", "Migrations", "1").exists())
            _verify_materialized_source(source, source_tree)
            marker_after = (
                None
                if not migration_marker.exists()
                else (migration_marker.stat().st_size, migration_marker.stat().st_mtime_ns)
            )
            self.assertEqual(marker_before, marker_after)

    def test_release_pipeline_requires_and_attests_the_trusted_public_symbol_gate(self) -> None:
        release = (REPOSITORY_ROOT / "scripts" / "release.ps1").read_text(
            encoding="utf-8"
        )
        reference = "-Arguments @('reference', '-Mode', 'Verify')"
        gate = "'compatibility-gate'"
        build = "-Arguments @('build', '-NoRestore', '-RequireEnergyPlus')"
        self.assertIn("'upstream'", release)
        self.assertIn("'--collect-evidence'", release)
        self.assertIn("$upstreamRoot", release)
        self.assertIn("$upstreamGatePath", release)
        self.assertIn("goniegonie.upstream-compatibility-report.v2", release)
        self.assertLess(release.index(reference), release.index(gate))
        self.assertLess(release.index(gate), release.index(build))
        self.assertIn("upstreamPublicSymbolCompatibility", release)
        self.assertNotIn("[bool] $upstreamCompatibility", release)
        for exact_check in (
            "Assert-JsonTrue -Value $item.Value",
            "evidence_execution.authoritative",
            "$requiredAssertionCount -le 0",
            "$requiredAssertionCount -ne $collectedAssertionCount",
            "$classificationTotal -ne 1242",
            "$classificationCounts.needs_reverification -ne 0",
            "Assert-JsonEmptyArray -Value $item.Value",
            "Read-JsonBytesOnce -Path $upstreamGatePath",
            "Assert-NoDuplicateJsonObjectKeys -Text $text",
            "Write-BytesAtomicallyExclusive",
            "$upstreamGateCopiedSha256 -cne $upstreamGateRead.sha256",
            "sha256 = $upstreamGateCopiedSha256",
            "Get-TrustedEvidenceReportSession",
            "Copy-TrustedEvidenceSession",
            "result_artifact_sha256s",
            "target_frameworks",
            "artifact_count",
            "$trustedEvidenceReleaseRoot",
            "authority-receipt.json",
            "artifact-index.json",
            "Get-ChildItem -LiteralPath $trustedEvidenceReleaseRoot -File -Recurse",
            "Get-TrustedEvidenceCanonicalReceiptMap",
            "Get-TrustedEvidenceCanonicalDataSha256",
            "-ExpectedAssertionIds $requiredAssertionIds",
            "parent_validation_build_props",
            "receipt/index request hashes do not match actual q.json bytes",
            "receipt/index child-result hashes do not match actual z.json bytes",
        ):
            self.assertIn(exact_check, release)

        powershell = shutil.which("powershell") or shutil.which("pwsh")
        if powershell is None:
            self.skipTest("PowerShell is unavailable")
        path = (REPOSITORY_ROOT / "scripts" / "release.ps1").as_posix().replace(
            "'", "''"
        )
        probe = rf"""
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    '{path}', [ref] $tokens, [ref] $errors)
if ($errors.Count -ne 0) {{ exit 10 }}
$names = @(
    'Assert-NoDuplicateJsonObjectKeys',
    'Assert-JsonTrue',
    'Get-JsonInteger',
    'Assert-JsonEmptyArray'
)
$definitions = $ast.FindAll({{
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $names -contains $node.Name
}}, $true)
foreach ($definition in $definitions) {{
    . ([scriptblock]::Create($definition.Extent.Text))
}}
if ($definitions.Count -ne $names.Count) {{ exit 11 }}
$rejected = $false
try {{
    Assert-NoDuplicateJsonObjectKeys `
        -Text '{{"gate":{{"passed":true,"\u0070assed":false}}}}'
}}
catch {{ $rejected = $true }}
if (-not $rejected) {{ exit 15 }}
Assert-NoDuplicateJsonObjectKeys -Text '{{"gate":{{"passed":true}}}}'
$invalidScalars = @('NaN', 'Infinity', '-Infinity', '01', '+1', '.1', '1.')
foreach ($invalidScalar in $invalidScalars) {{
    $rejected = $false
    try {{
        Assert-NoDuplicateJsonObjectKeys `
            -Text ('{{"value":' + $invalidScalar + '}}')
    }}
    catch {{ $rejected = $true }}
    if (-not $rejected) {{ exit 16 }}
}}
Assert-NoDuplicateJsonObjectKeys `
    -Text '{{"values":[null,true,false,0,-1,1.25,2e3,-4.5E-2]}}'
$rejected = $false
try {{ Assert-JsonTrue -Value 'false' -Label fixture }}
catch {{ $rejected = $true }}
if (-not $rejected) {{ exit 12 }}
Assert-JsonTrue -Value $true -Label fixture
$rejected = $false
try {{ $null = Get-JsonInteger -Value '1242' -Label fixture }}
catch {{ $rejected = $true }}
if (-not $rejected) {{ exit 13 }}
$rejected = $false
try {{ Assert-JsonEmptyArray -Value 'not-an-array' -Label fixture }}
catch {{ $rejected = $true }}
if (-not $rejected) {{ exit 14 }}
"""
        completed = subprocess.run(
            [powershell, "-NoProfile", "-NonInteractive", "-Command", probe],
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(
            0,
            completed.returncode,
            msg=f"PowerShell release-gate probe failed:\n{completed.stdout}\n{completed.stderr}",
        )

    def test_theory_cases_bind_exact_trx_codebase_records_and_output(self) -> None:
        with TemporaryWorkspace() as workspace:
            fixture = self._write_artifacts(workspace, theory=True)

            parsed = _parse_project_artifacts(**fixture)

            self.assertEqual(1, len(parsed["assertions"]))
            assertion = parsed["assertions"][0]
            self.assertEqual("passed", assertion["outcome"])
            self.assertFalse(assertion["structural_only"])
            self.assertEqual(
                _sha256_data(
                    {
                        "cases": [
                            {
                                "output": {"value": 1},
                                "test_case": "Product.Tests.ParityTests.Matches(value: 1)",
                            },
                            {
                                "output": {"value": 2},
                                "test_case": "Product.Tests.ParityTests.Matches(value: 2)",
                            },
                        ]
                    }
                ),
                assertion["output_sha256"],
            )
            self.assertEqual(2, len(parsed["records"]))
            self.assertTrue(parsed["test_dll"]["path"].endswith("Product.Tests.dll"))
            self.assertEqual(1, len(parsed["implementation_dlls"]))

    def test_foreign_codebase_structural_record_and_undeclared_record_fail_closed(self) -> None:
        with TemporaryWorkspace() as workspace:
            foreign = self._write_artifacts(workspace, codebase="foreign")
            with self.assertRaisesRegex(TrustedCollectorError, "not the fresh test DLL"):
                _parse_project_artifacts(**foreign)

        with TemporaryWorkspace() as workspace:
            structural = self._write_artifacts(workspace, structural_only=True)
            with self.assertRaisesRegex(TrustedCollectorError, "structural-only"):
                _parse_project_artifacts(**structural)

        with TemporaryWorkspace() as workspace:
            extra = self._write_artifacts(workspace)
            record_root = extra["records_root"]
            self._write_json(
                record_root / "foreign.json",
                self._record("foreign-assertion", "Foreign.Case", {"value": 3}),
            )
            with self.assertRaisesRegex(TrustedCollectorError, "undeclared or unexecuted"):
                _parse_project_artifacts(**extra)

    def test_trx_dtd_entity_and_json_duplicate_keys_fail_before_parsing(self) -> None:
        with TemporaryWorkspace() as workspace:
            fixture = self._write_artifacts(workspace)
            trx = fixture["trx_path"]
            text = trx.read_text(encoding="utf-8")
            trx.write_text(
                text.replace(
                    "<TestRun ",
                    '<!DOCTYPE TestRun [<!ENTITY forged "value">]>\n<TestRun ',
                    1,
                ),
                encoding="utf-8",
                newline="\n",
            )
            with self.assertRaisesRegex(TrustedCollectorError, "DOCTYPE or ENTITY"):
                _parse_project_artifacts(**fixture)

        with self.assertRaisesRegex(TrustedCollectorError, "duplicate key 'assertion_id'"):
            _json_loads('{"assertion_id":"first","assertion_id":"forged"}')

        with TemporaryWorkspace() as workspace:
            project = workspace.write(
                "src/Adversarial.csproj",
                '<!DOCTYPE Project [<!ENTITY forged "value">]>\n'
                '<Project><PropertyGroup><AssemblyName>&forged;</AssemblyName>'
                '</PropertyGroup></Project>\n',
            )
            with self.assertRaisesRegex(TrustedCollectorError, "DOCTYPE or ENTITY"):
                _project_assembly_name(workspace.path, project.relative_to(workspace.path).as_posix())

        with TemporaryWorkspace() as workspace:
            fixture = self._write_artifacts(workspace)
            trx = fixture["trx_path"]
            text = trx.read_text(encoding="utf-8")
            concrete = str(next(fixture["bin_root"].rglob("Product.Tests.dll")))
            trx.write_text(
                text.replace(concrete, "*.dll"),
                encoding="utf-8",
                newline="\n",
            )
            with self.assertRaisesRegex(TrustedCollectorError, "literal path"):
                _parse_project_artifacts(**fixture)

        with TemporaryWorkspace() as workspace:
            fixture = self._write_artifacts(workspace)
            trx = fixture["trx_path"]
            text = trx.read_text(encoding="utf-8")
            concrete_path = next(fixture["bin_root"].rglob("Product.Tests.dll"))
            concrete = str(concrete_path)
            exact_relative = concrete_path.relative_to(fixture["bin_root"]).as_posix()
            trx.write_text(
                text.replace(concrete, exact_relative),
                encoding="utf-8",
                newline="\n",
            )
            self.assertEqual(1, len(_parse_project_artifacts(**fixture)["assertions"]))

            # A basename is not a literal path to the nested DLL.  The
            # collector must not recursively search for a unique match.
            trx.write_text(
                text.replace(concrete, "Product.Tests.dll"),
                encoding="utf-8",
                newline="\n",
            )
            with self.assertRaisesRegex(TrustedCollectorError, "TRX codeBase"):
                _parse_project_artifacts(**fixture)

    def test_reparse_record_directory_is_rejected_at_artifact_use(self) -> None:
        with TemporaryWorkspace() as workspace:
            fixture = self._write_artifacts(workspace)
            link = fixture["records_root"].parent / "records-link"
            try:
                os.symlink(fixture["records_root"], link, target_is_directory=True)
            except OSError as exception:
                self.skipTest(f"directory symlinks unavailable: {exception}")
            fixture["records_root"] = link
            with self.assertRaisesRegex(
                TrustedCollectorError,
                "symlink, junction, or reparse point",
            ):
                _parse_project_artifacts(**fixture)

    def test_hmac_tampering_is_rejected_without_creating_authority(self) -> None:
        with TemporaryWorkspace() as workspace:
            request = self._empty_request(workspace)
            secret = b"s" * 32
            signed = {
                "payload": {},
                "result_hmac": _hmac_sha256(secret, b"not-the-payload"),
            }
            with self.assertRaisesRegex(TrustedCollectorError, "HMAC is invalid"):
                _validate_child_result_artifacts(request, signed, secret)

    def test_windows_ambiguous_relative_paths_fail_closed(self) -> None:
        for value in (
            "src/con.cs",
            "src/aux.txt",
            "src/name. /file.cs",
            "src/name./file.cs",
            "src/file.cs:stream",
            "src/double//file.cs",
            "src/trailing/",
            "src/question?.cs",
        ):
            with self.subTest(value=value):
                with self.assertRaises(TrustedCollectorError):
                    _relative_path(value, "adversarial path")

    def test_dotnet_build_cannot_inherit_targets_or_restore_injection(self) -> None:
        injected = {
            "DirectoryBuildPropsPath": "C:/forged/props",
            "DirectoryBuildTargetsPath": "C:/forged/targets",
            "CustomBeforeMicrosoftCommonTargets": "C:/forged/before.targets",
            "CustomAfterMicrosoftCommonTargets": "C:/forged/after.targets",
            "RestoreSources": "https://forged.invalid/index.json",
            "MSBuildSDKsPath": "C:/forged/sdks",
            "NUGET_PACKAGES": "C:/forged/packages",
        }
        original = dict(os.environ)
        try:
            os.environ.update(injected)
            environment = _sanitized_environment()
        finally:
            os.environ.clear()
            os.environ.update(original)
        for name in injected:
            self.assertNotIn(name, environment)

        with TemporaryWorkspace() as workspace:
            request = self._empty_request(workspace)
            request["source"]["files"].extend(
                (
                    {"path": "Directory.Build.props", "sha256": HASH_A},
                    {"path": "Directory.Packages.props", "sha256": HASH_A},
                    {"path": "NuGet.config", "sha256": HASH_A},
                )
            )
            request["source"]["files"].sort(key=lambda item: item["path"])
            project = {
                "path": "tests/Product.Tests/Product.Tests.csproj",
                "slug": "product-tests",
            }
            build_props_path = Path(request["session_directory"]) / "c" / "product-tests" / "d.props"
            build_props_path.parent.mkdir(parents=True)
            build_props_path.write_text("<Project />\n", encoding="utf-8", newline="\n")
            project["build_props"] = {
                "bytes": build_props_path.stat().st_size,
                "path": "c/product-tests/d.props",
                "sha256": _sha256_bytes(build_props_path.read_bytes()),
            }
            source_root = Path(request["source"]["root"])
            command = _dotnet_test_command(
                request,
                project,
                workspace.path / "bin",
                workspace.path / "obj",
                workspace.path / "results",
            )
            restore_command = _dotnet_restore_command(request, project)
            self.assertIn(
                "-p:CustomBeforeMicrosoftCommonTargets="
                f"{source_root / '.goniegonie-no-custom-before.targets'}",
                command,
            )
            self.assertIn(
                "-p:CustomAfterMicrosoftCommonTargets="
                f"{source_root / '.goniegonie-no-custom-after.targets'}",
                command,
            )
            self.assertIn("-p:ImportDirectoryBuildTargets=false", command)
            self.assertIn(
                f"-p:DirectoryBuildPropsPath={build_props_path}",
                command,
            )
            self.assertIn(
                f"-p:DirectoryPackagesPropsPath={source_root / 'Directory.Packages.props'}",
                command,
            )
            self.assertIn(
                f"-p:RestoreConfigFile={source_root / 'NuGet.config'}",
                command,
            )
            self.assertFalse(any("forged" in item for item in command))
            self.assertFalse(any("forged" in item for item in restore_command))
            self.assertIn("--locked-mode", restore_command)
            self.assertIn("--no-restore", command)
            self.assertFalse(
                any(
                    item.startswith("-p:TargetFramework")
                    for item in restore_command
                )
            )

    def test_test_process_cannot_preempt_restore_provenance_artifacts(self) -> None:
        with TemporaryWorkspace() as workspace:
            session = workspace.path / "session"
            source = session / "s"
            source.mkdir(parents=True)
            slug = "product-tests"
            build_props_path = session / "c" / slug / "d.props"
            build_props_path.parent.mkdir(parents=True)
            build_props_path.write_bytes(b"<Project />\n")
            project = {
                "build_props": {
                    "bytes": build_props_path.stat().st_size,
                    "path": f"c/{slug}/d.props",
                    "sha256": _sha256_bytes(build_props_path.read_bytes()),
                },
                "path": "tests/Product.Tests/Product.Tests.csproj",
                "slug": slug,
            }
            dotnet = Path(sys.executable).resolve(strict=True)
            request = {
                "dotnet": {
                    "path": dotnet.as_posix(),
                    "sdk_root": dotnet.parent.as_posix(),
                },
                "nonce": NONCE,
                "session_directory": session.as_posix(),
                "source": {"files": [], "root": source.as_posix()},
                "target_framework": "net8.0-windows",
            }
            calls = 0
            planted = session / "p" / slug / "restore.stdout.bin"

            def fake_run(command: list[str], **_: object) -> subprocess.CompletedProcess[bytes]:
                nonlocal calls
                calls += 1
                if calls == 1:
                    self.assertEqual("restore", command[1])
                    self.assertFalse(planted.exists())
                    return subprocess.CompletedProcess(
                        command,
                        0,
                        stdout=b"trusted restore stdout",
                        stderr=b"",
                    )
                self.assertEqual("test", command[1])
                self.assertFalse(planted.exists())
                planted.write_bytes(b"forged by test process")
                return subprocess.CompletedProcess(
                    command,
                    0,
                    stdout=b"test stdout",
                    stderr=b"",
                )

            with self.assertRaisesRegex(
                TrustedCollectorError,
                "restore provenance path was preempted",
            ):
                _run_test_project(request, project, run_command=fake_run)
            self.assertEqual(2, calls)
            self.assertEqual(b"forged by test process", planted.read_bytes())

    def test_test_process_cannot_preempt_test_output_provenance_artifacts(self) -> None:
        with TemporaryWorkspace() as workspace:
            session = workspace.path / "session"
            source = session / "s"
            source.mkdir(parents=True)
            slug = "product-tests"
            build_props_path = session / "c" / slug / "d.props"
            build_props_path.parent.mkdir(parents=True)
            build_props_path.write_bytes(b"<Project />\n")
            project = {
                "build_props": {
                    "bytes": build_props_path.stat().st_size,
                    "path": f"c/{slug}/d.props",
                    "sha256": _sha256_bytes(build_props_path.read_bytes()),
                },
                "path": "tests/Product.Tests/Product.Tests.csproj",
                "slug": slug,
            }
            dotnet = Path(sys.executable).resolve(strict=True)
            request = {
                "dotnet": {
                    "path": dotnet.as_posix(),
                    "sdk_root": dotnet.parent.as_posix(),
                },
                "nonce": NONCE,
                "session_directory": session.as_posix(),
                "source": {"files": [], "root": source.as_posix()},
                "target_framework": "net8.0-windows",
            }
            calls = 0
            planted = session / "p" / slug / "stdout.bin"

            def fake_run(command: list[str], **_: object) -> subprocess.CompletedProcess[bytes]:
                nonlocal calls
                calls += 1
                if calls == 1:
                    self.assertEqual("restore", command[1])
                    return subprocess.CompletedProcess(
                        command,
                        0,
                        stdout=b"trusted restore stdout",
                        stderr=b"",
                    )
                self.assertEqual("test", command[1])
                planted.write_bytes(b"forged test stdout")
                return subprocess.CompletedProcess(
                    command,
                    0,
                    stdout=b"trusted test stdout",
                    stderr=b"",
                )

            with self.assertRaisesRegex(
                TrustedCollectorError,
                "test provenance path was preempted",
            ):
                _run_test_project(request, project, run_command=fake_run)
            self.assertEqual(2, calls)
            self.assertEqual(b"forged test stdout", planted.read_bytes())

    def test_captured_output_descriptor_never_adopts_later_disk_bytes(self) -> None:
        with TemporaryWorkspace() as workspace:
            session = workspace.path / "session"
            path = session / "p" / "project" / "stdout.bin"
            path.parent.mkdir(parents=True)
            captured = b"captured subprocess bytes"
            path.write_bytes(captured)

            descriptor = _captured_session_artifact(
                session,
                path,
                captured,
                "captured stdout",
            )
            self.assertEqual(len(captured), descriptor["bytes"])
            self.assertEqual(_sha256_bytes(captured), descriptor["sha256"])

            path.write_bytes(b"x" * len(captured))
            self.assertEqual(_sha256_bytes(captured), descriptor["sha256"])
            with self.assertRaisesRegex(TrustedCollectorError, "hash is invalid"):
                _validate_session_artifact(session, descriptor, "captured stdout")

    def test_failed_dotnet_exit_cannot_produce_a_passing_assertion(self) -> None:
        with TemporaryWorkspace() as workspace:
            fixture = self._write_artifacts(workspace, exit_code=1)

            assertion = _parse_project_artifacts(**fixture)["assertions"][0]

            self.assertEqual("failed", assertion["outcome"])
            self.assertFalse(assertion["skipped"])

    def test_request_rejects_escaped_sessions_and_inexact_package_lock_closure(self) -> None:
        with TemporaryWorkspace() as workspace:
            request = self._empty_request(workspace)
            normalized = _validate_request(request)
            self.assertEqual([], normalized["package_locks"])

            forged_count = json.loads(json.dumps(request))
            forged_count["project_count"] = True
            with self.assertRaisesRegex(TrustedCollectorError, "project_count is invalid"):
                _validate_request(forged_count)

            mismatched_count = json.loads(json.dumps(request))
            mismatched_count["assertion_count"] = 1
            with self.assertRaisesRegex(TrustedCollectorError, "assertion_count is invalid"):
                _validate_request(mismatched_count)

            escaped = json.loads(json.dumps(request))
            escaped["session_directory"] = str(workspace.path / "elsewhere" / SESSION_ID)
            Path(escaped["session_directory"]).mkdir(parents=True)
            with self.assertRaisesRegex(TrustedCollectorError, "escaped"):
                _validate_request(escaped)

            stale_locks = json.loads(json.dumps(request))
            stale_locks["inputs"].append(
                {"path": "tests/Product.Tests/packages.lock.json", "sha256": HASH_A}
            )
            stale_locks["inputs"].sort(key=lambda item: item["path"])
            with self.assertRaisesRegex(TrustedCollectorError, "package-lock closure"):
                _validate_request(stale_locks)

    def test_child_declared_counts_are_strict_and_request_bound(self) -> None:
        with TemporaryWorkspace() as workspace:
            request = self._empty_request(workspace)
            payload = {
                "artifact_count": True,
                "assertion_count": 1,
                "assertions": [],
                "git_executable_sha256": request["git"]["sha256"],
                "inputs": request["inputs"],
                "nonce": request["nonce"],
                "package_locks": request["package_locks"],
                "project_count": 1,
                "projects": [],
                "repository_head": request["repository_head"],
                "request_sha256": HASH_A,
                "schema": trusted_collector.CHILD_RESULT_SCHEMA,
                "session_id": request["session_id"],
                "source_tree_sha256": request["source"]["sha256"],
                "target_framework": request["target_framework"],
                "toolchain_manifest_sha256": request["dotnet"]["sdk_manifest"]["sha256"],
            }
            secret = b"s" * 32
            signed = {
                "payload": payload,
                "result_hmac": _hmac_sha256(secret, self._canonical(payload)),
            }
            with self.assertRaisesRegex(TrustedCollectorError, "artifact_count is invalid"):
                _validate_child_result_artifacts(request, signed, secret)

            payload["artifact_count"] = 1
            signed["result_hmac"] = _hmac_sha256(secret, self._canonical(payload))
            with self.assertRaisesRegex(TrustedCollectorError, "stale assertion_count"):
                _validate_child_result_artifacts(request, signed, secret)

    def test_isolated_manifest_content_hashes_are_cross_bound_to_request(self) -> None:
        with TemporaryWorkspace() as workspace:
            source = workspace.path / "source"
            upstream = source / "upstream"
            upstream.mkdir(parents=True)
            inventory_content = {
                "files": [],
                "scope_sha256": HASH_A,
                "symbols": [],
                "upstream_commit": COMMIT,
            }
            inventory_hash = _sha256_data(inventory_content)
            inventory = {
                "content_sha256": inventory_hash,
                **inventory_content,
                "schema": "goniegonie.upstream-public-symbol-inventory.v2",
                "summary": {},
            }
            evidence_content = {
                "entries": [],
                "inventory_sha256": inventory_hash,
                "upstream_commit": COMMIT,
            }
            evidence_hash = _sha256_data(evidence_content)
            evidence = {
                "content_sha256": evidence_hash,
                **evidence_content,
                "schema": "goniegonie.upstream-symbol-evidence.v1",
                "summary": {},
            }
            self._write_json(upstream / "public-symbol-inventory.json", inventory)
            self._write_json(upstream / "symbol-evidence.json", evidence)
            matrix_content = {
                "classifications": [],
                "details": [],
                "entry_order": "public-symbol-inventory.symbols",
                "inventory_sha256": inventory_hash,
                "needs_reverification_rationale": (
                    "No symbol-level equivalence, verified exception, or out-of-scope evidence is registered."
                ),
                "upstream_commit": COMMIT,
            }
            matrix_hash = _sha256_data(matrix_content)
            matrix = {
                "content_sha256": matrix_hash,
                **matrix_content,
                "schema": "goniegonie.upstream-compatibility-matrix.v1",
                "summary": {},
            }
            self._write_json(upstream / "compatibility-matrix.json", matrix)
            binding = {
                "inventory_sha256": inventory_hash,
                "matrix_sha256": matrix_hash,
                "symbol_evidence_sha256": evidence_hash,
                "upstream_commit": COMMIT,
            }

            _verify_canonical_evidence_binding(source, binding)

            swapped = dict(binding)
            swapped["inventory_sha256"] = HASH_A
            with self.assertRaisesRegex(TrustedCollectorError, "does not match"):
                _verify_canonical_evidence_binding(source, swapped)

    def test_constructor_subclass_and_removed_standalone_issuers_cannot_forge_authority(self) -> None:
        base, registry = self._base_results_and_registry()
        forged = TrustedEvidenceResults(
            base.upstream_commit,
            base.inventory_sha256,
            base.symbol_evidence_sha256,
            base.collector_path,
            base.collector_symbol,
            base.collector_source_sha256,
            base.assertions,
            "net8.0-windows",
            HASH_A,
            "temp/u/" + SESSION_ID + "/a.json",
            HASH_B,
            "temp/u/" + SESSION_ID + "/i.json",
            SESSION_ID,
            1,
            1,
            1,
            HASH_C,
        )
        self.assertFalse(is_authoritative_evidence_results(forged))
        self.assertIsNone(authority_receipt_sha256(forged))
        self.assertFalse(hasattr(trusted_collector, "_issue_validated_results"))
        self.assertFalse(hasattr(trusted_collector, "_validate_and_authorize_child"))

        external_execution = evaluate_evidence_execution(
            registry,
            ("service-parity",),
            (base,),
        )
        self.assertTrue(external_execution.assertions_satisfied)
        self.assertFalse(external_execution.authoritative)
        self.assertFalse(external_execution.passed)
        self.assertEqual(("net8.0-windows",), external_execution.target_frameworks)
        self.assertEqual((), external_execution.result_artifacts)

    def test_repository_snapshot_rejects_dirty_assume_unchanged_and_hardlinks(self) -> None:
        with TemporaryWorkspace() as workspace:
            tracked = workspace.write("tracked.txt", "exact\n")
            self._commit(workspace.path)
            self.assertEqual(COMMIT.__len__(), len(_exact_repository_snapshot(workspace.path)[0]))
            tracked.write_text("dirty\n", encoding="utf-8", newline="\n")
            with self.assertRaisesRegex(TrustedCollectorError, "clean repository"):
                _exact_repository_snapshot(workspace.path)

        with TemporaryWorkspace() as workspace:
            tracked = workspace.write("tracked.txt", "exact\n")
            self._commit(workspace.path)
            subprocess.run(
                ["git", "update-index", "--assume-unchanged", "tracked.txt"],
                cwd=workspace.path,
                check=True,
            )
            tracked.write_text("forged\n", encoding="utf-8", newline="\n")
            with self.assertRaisesRegex(TrustedCollectorError, "assume-unchanged"):
                _exact_repository_snapshot(workspace.path)

        with TemporaryWorkspace() as workspace, TemporaryWorkspace() as outside:
            tracked = workspace.write("tracked.txt", "exact\n")
            self._commit(workspace.path)
            victim = outside.write("victim.txt", "exact\n")
            tracked.unlink()
            try:
                import os

                os.link(victim, tracked)
            except OSError as exception:
                self.skipTest(f"hardlinks unavailable: {exception}")
            subprocess.run(
                ["git", "update-index", "--assume-unchanged", "tracked.txt"],
                cwd=workspace.path,
                check=True,
            )
            # Remove the flag again so the link-count check, not the index flag,
            # is the reason this exact-byte replacement is rejected.
            subprocess.run(
                ["git", "update-index", "--no-assume-unchanged", "tracked.txt"],
                cwd=workspace.path,
                check=True,
            )
            with self.assertRaisesRegex(TrustedCollectorError, "hardlinked"):
                _exact_repository_snapshot(workspace.path)

    def test_snapshot_uses_hash_bound_absolute_git_without_path_lookup(self) -> None:
        with TemporaryWorkspace() as workspace:
            workspace.write("tracked.txt", "exact\n")
            self._commit(workspace.path)
            git = _resolve_git()
            original_path = os.environ.get("PATH")
            try:
                os.environ["PATH"] = ""
                head, files = _exact_repository_snapshot(workspace.path, git)
            finally:
                if original_path is None:
                    os.environ.pop("PATH", None)
                else:
                    os.environ["PATH"] = original_path
            self.assertRegex(head, r"^[0-9a-f]{40}$")
            self.assertEqual(["tracked.txt"], list(files))

        with TemporaryWorkspace() as workspace:
            request = self._empty_request(workspace)
            request["git"]["sha256"] = HASH_A
            with self.assertRaisesRegex(TrustedCollectorError, "git executable hash is stale"):
                _validate_request(request)

    def test_ignored_default_compile_injection_is_absent_from_isolated_head_tree(self) -> None:
        with TemporaryWorkspace() as workspace, TemporaryWorkspace() as isolated:
            workspace.write(
                "src/Product/Product.csproj",
                '<Project Sdk="Microsoft.NET.Sdk"></Project>\n',
            )
            workspace.write("src/Product/Real.cs", "public class Real { }\n")
            self._commit(workspace.path)
            info_exclude = workspace.path / ".git" / "info" / "exclude"
            with info_exclude.open("a", encoding="utf-8", newline="\n") as stream:
                stream.write("src/Product/Injected.cs\n")
            workspace.write(
                "src/Product/Injected.cs",
                "public class Injected { public static bool Forged => true; }\n",
            )

            _, tracked = _exact_repository_snapshot(workspace.path)
            self.assertNotIn("src/Product/Injected.cs", tracked)
            source_root = isolated.path / "source"
            descriptor = _materialize_source_tree(workspace.path, source_root, tracked)
            self.assertFalse((source_root / "src" / "Product" / "Injected.cs").exists())
            _verify_materialized_source(source_root, descriptor)

            (source_root / "src" / "Product" / "Injected.cs").write_text(
                "public class Injected { }\n",
                encoding="utf-8",
                newline="\n",
            )
            with self.assertRaisesRegex(TrustedCollectorError, "exact HEAD file set"):
                _verify_materialized_source(source_root, descriptor)

    def _write_artifacts(
        self,
        workspace: TemporaryWorkspace,
        *,
        theory: bool = False,
        codebase: str = "test",
        structural_only: bool = False,
        exit_code: int = 0,
    ) -> dict:
        session = workspace.path / "session"
        base = session / "projects" / "product-tests"
        bin_root = base / "bin"
        results_root = base / "results"
        records_root = base / "records"
        for path in (bin_root, results_root, records_root):
            path.mkdir(parents=True)
        test_dll = bin_root / "Release" / "net8.0-windows" / "Product.Tests.dll"
        implementation_dll = bin_root / "Release" / "net8.0-windows" / "Product.dll"
        test_dll.parent.mkdir(parents=True)
        test_dll.write_bytes(b"fresh-test-assembly")
        implementation_dll.write_bytes(b"fresh-product-assembly")
        foreign_dll = bin_root / "Release" / "net8.0-windows" / "Foreign.dll"
        foreign_dll.write_bytes(b"foreign")
        code_base = test_dll if codebase == "test" else foreign_dll
        cases = [
            ("case-1", "Product.Tests.ParityTests.Matches", {"value": 1}),
        ]
        if theory:
            cases = [
                (
                    "case-1",
                    "Product.Tests.ParityTests.Matches(value: 1)",
                    {"value": 1},
                ),
                (
                    "case-2",
                    "Product.Tests.ParityTests.Matches(value: 2)",
                    {"value": 2},
                ),
            ]
        definitions = []
        results = []
        for index, (test_id, test_name, output) in enumerate(cases, start=1):
            execution_id = f"execution-{index}"
            definitions.append(
                f'''<UnitTest id="{test_id}" name="{escape(test_name)}">
  <Execution id="{execution_id}" />
  <TestMethod codeBase="{escape(str(code_base))}" className="Product.Tests.ParityTests" name="Matches" />
</UnitTest>'''
            )
            results.append(
                f'<UnitTestResult executionId="{execution_id}" testId="{test_id}" '
                f'testName="{escape(test_name)}" outcome="Passed" />'
            )
            self._write_json(
                records_root / f"{test_id}.json",
                self._record("service-parity", test_name, output, structural_only),
            )
        trx = results_root / "product-tests.trx"
        trx.write_text(
            f'''<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <TestDefinitions>{''.join(definitions)}</TestDefinitions>
  <Results>{''.join(results)}</Results>
</TestRun>
''',
            encoding="utf-8",
            newline="\n",
        )
        request = {"nonce": NONCE, "session_directory": session.as_posix()}
        project = {
            "assembly_name": "Product.Tests",
            "assertions": [
                {
                    "exercised_load": "not_applicable",
                    "id": "service-parity",
                    "test_path": TEST_PATH,
                    "test_source_sha256": HASH_A,
                    "test_symbol": TEST_SYMBOL,
                }
            ],
            "implementation_assemblies": ["Product"],
        }
        return {
            "bin_root": bin_root,
            "exit_code": exit_code,
            "project": project,
            "records_root": records_root,
            "request": request,
            "trx_path": trx,
        }

    def _empty_request(
        self,
        workspace: TemporaryWorkspace,
        *,
        session_id: str = SESSION_ID,
    ) -> dict:
        root = workspace.path
        session = root / "temp" / "u" / session_id
        session.mkdir(parents=True)
        collector = workspace.write(
            "tools/upstream-tracker/goniegonie_upstream_tracker/trusted_collector.py",
            "def collect_trusted_evidence():\n    pass\n",
        )
        collector_hash = _sha256_bytes(collector.read_bytes())
        source_root = session / "s"
        source_collector = source_root / "tools" / "upstream-tracker" / "goniegonie_upstream_tracker" / "trusted_collector.py"
        source_collector.parent.mkdir(parents=True)
        source_collector.write_bytes(collector.read_bytes())
        source_files = [
            {
                "path": "tools/upstream-tracker/goniegonie_upstream_tracker/trusted_collector.py",
                "sha256": collector_hash,
            }
        ]
        dotnet_path = Path(sys.executable).resolve(strict=True)
        sdk_file = dotnet_path
        sdk_entry = {
            "bytes": sdk_file.stat().st_size,
            "path": sdk_file.name,
            "sha256": _sha256_bytes(sdk_file.read_bytes()),
        }
        sdk_content = {
            "files": [sdk_entry],
            "root": dotnet_path.parent.as_posix(),
        }
        sdk_manifest = {
            **sdk_content,
            "file_count": 1,
            "schema": trusted_collector.SDK_MANIFEST_SCHEMA,
            "sha256": _sha256_data(sdk_content),
        }
        git_path = _resolve_git()
        return {
            "assertion_count": 0,
            "dotnet": {
                "path": dotnet_path.as_posix(),
                "sdk_manifest": sdk_manifest,
                "sdk_root": dotnet_path.parent.as_posix(),
                "sdk_version": "8.0.424",
                "sha256": _sha256_bytes(dotnet_path.read_bytes()),
            },
            "evidence_binding": {
                "collector_path": "tools/upstream-tracker/goniegonie_upstream_tracker/trusted_collector.py",
                "collector_source_sha256": collector_hash,
                "collector_symbol": "collect_trusted_evidence",
                "inventory_sha256": HASH_A,
                "matrix_sha256": HASH_C,
                "symbol_evidence_sha256": HASH_B,
                "upstream_commit": COMMIT,
            },
            "git": {
                "path": git_path.as_posix(),
                "sha256": _sha256_bytes(git_path.read_bytes()),
            },
            "inputs": [
                {
                    "path": "tools/upstream-tracker/goniegonie_upstream_tracker/trusted_collector.py",
                    "sha256": collector_hash,
                }
            ],
            "nonce": NONCE,
            "package_locks": [],
            "project_count": 0,
            "projects": [],
            "repository_head": COMMIT,
            "repository_root": root.as_posix(),
            "required_assertion_ids": [],
            "schema": REQUEST_SCHEMA,
            "session_directory": session.as_posix(),
            "session_id": session_id,
            "source": {
                "file_count": 1,
                "files": source_files,
                "root": source_root.as_posix(),
                "sha256": _sha256_data({"files": source_files}),
            },
            "target_framework": "net8.0-windows",
        }

    @staticmethod
    def _record(
        assertion_id: str,
        test_case: str,
        output: object,
        structural_only: bool = False,
    ) -> dict:
        return {
            "assertion_id": assertion_id,
            "exercised_load": "not_applicable",
            "output": output,
            "schema": RECORD_SCHEMA,
            "session_nonce": NONCE,
            "structural_only": structural_only,
            "test_case": test_case,
        }

    @staticmethod
    def _write_json(path: Path, value: object) -> None:
        path.write_text(
            json.dumps(value, ensure_ascii=False, sort_keys=True),
            encoding="utf-8",
            newline="\n",
        )

    @staticmethod
    def _canonical(value: object) -> bytes:
        return json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")

    @staticmethod
    def _base_results_and_registry() -> tuple[EvidenceResults, SymbolEvidenceRegistry]:
        receipt = EvidenceReceipt(
            "service-parity",
            TEST_PATH,
            TEST_SYMBOL,
            HASH_A,
            "Exact deterministic behavioral output.",
            "cross_language",
            "passed",
            False,
            False,
            "not_applicable",
            False,
            HASH_B,
        )
        registry = SymbolEvidenceRegistry(
            COMMIT,
            HASH_A,
            (
                SymbolEvidence(
                    "src/source/service.py",
                    "Service.run",
                    HASH_C,
                    "src/Product/Service.cs",
                    "Product.Service.Run",
                    HASH_A,
                    (receipt,),
                ),
            ),
        )
        assertion = ExecutedAssertion(
            "service-parity",
            TEST_PATH,
            TEST_SYMBOL,
            HASH_A,
            "passed",
            False,
            False,
            "not_applicable",
            HASH_B,
        )
        results = EvidenceResults(
            COMMIT,
            HASH_A,
            registry.content_sha256,
            "collector.py",
            "collect",
            HASH_C,
            (assertion,),
            "net8.0-windows",
        )
        return results, registry

    @staticmethod
    def _commit(repository: Path) -> None:
        subprocess.run(["git", "init", "--quiet"], cwd=repository, check=True)
        subprocess.run(
            ["git", "config", "core.autocrlf", "false"],
            cwd=repository,
            check=True,
        )
        subprocess.run(["git", "add", "--all"], cwd=repository, check=True)
        subprocess.run(
            [
                "git",
                "-c",
                "user.name=GonieGonie Test",
                "-c",
                "user.email=test@goniegonie.invalid",
                "commit",
                "--quiet",
                "-m",
                "fixture",
            ],
            cwd=repository,
            check=True,
        )


if __name__ == "__main__":
    unittest.main()

#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
$settingsPath = Join-Path $repositoryRoot '.tools\state\local.settings.json'
$environmentStampPath = Join-Path $repositoryRoot '.tools\state\python-environment.json'
$requirementsPath = Join-Path $repositoryRoot 'tools\documentation\requirements.lock.txt'
$nugetConfigPath = Join-Path $repositoryRoot 'NuGet.config'
$nugetPackagesPath = Join-Path $repositoryRoot '.tools\nuget\packages'
$environmentVerifierPath = Join-Path $repositoryRoot 'tools\documentation\verify_environment.py'
$sourceVerifierPath = Join-Path $repositoryRoot 'tools\documentation\verify_repository_docs.py'
$guideBuilderPath = Join-Path $repositoryRoot 'tools\documentation\build_user_guide.py'
$guideMetadataPath = Join-Path $repositoryRoot 'tools\documentation\component-guides.json'
$catalogProjectPath = Join-Path $repositoryRoot 'tools\component-catalog\GonieGonie.Dragons.ComponentCatalog.csproj'
$catalogNet8AssemblyPath = Join-Path $repositoryRoot 'temp\build\bin\GonieGonie.Dragons.ComponentCatalog\Release\net8.0-windows\GonieGonie.Dragons.ComponentCatalog.dll'
$catalogNet48ExecutablePath = Join-Path $repositoryRoot 'temp\build\bin\GonieGonie.Dragons.ComponentCatalog\Release\net48\GonieGonie.Dragons.ComponentCatalog.exe'
$workRoot = Join-Path $repositoryRoot 'temp\documentation'
$catalogNet8Path = Join-Path $workRoot 'component-catalog.net8.0-windows.json'
$catalogNet7Path = Join-Path $workRoot 'component-catalog.net7.0-windows.json'
$catalogNet48Path = Join-Path $workRoot 'component-catalog.net48.json'
$logsRoot = Join-Path $workRoot 'logs'
$defaultOutputPath = Join-Path $repositoryRoot 'artifacts\documentation\Dragons-Grasshopper-User-Guide-0.1.0.pdf'

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = $defaultOutputPath
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
if (-not [System.IO.Path]::GetExtension($OutputPath).Equals(
    '.pdf',
    [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Documentation output must be a PDF: '$OutputPath'."
}
$safeOutputPath = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $OutputPath `
    -AllowedTopLevelNames @('artifacts')
$safeWorkRoot = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $workRoot `
    -AllowedTopLevelNames @('temp')
$outputDirectory = Split-Path -Parent $safeOutputPath
$referenceDirectory = Join-Path $repositoryRoot 'docs\user\user-guide'

# Lexical containment is not enough for a write workflow: a junction beneath
# temp, artifacts, or docs could redirect a later replace outside the checkout.
foreach ($path in @($safeWorkRoot, $outputDirectory, $referenceDirectory)) {
    Assert-NoReparsePoints -Path $path -AnchorPath $repositoryRoot
}

foreach ($requiredFile in @(
    $settingsPath,
    $environmentStampPath,
    $requirementsPath,
    $environmentVerifierPath,
    $sourceVerifierPath,
    $guideBuilderPath,
    $guideMetadataPath,
    $catalogProjectPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required documentation input is missing: '$requiredFile'. Run 'dev.cmd setup' first if local settings are absent."
    }
}
foreach ($stateFile in @($settingsPath, $environmentStampPath)) {
    Assert-NoReparsePoints -Path $stateFile -AnchorPath $repositoryRoot
}

$settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string] $settings.schema -cne 'goniegonie.dragons-grasshopper.local-settings.v1') {
    throw "Unsupported local settings schema. Run 'dev.cmd setup' again."
}
if ([string] $settings.dotnet.status -cne 'ready') {
    throw "The pinned .NET SDK is not ready. Run 'dev.cmd setup'."
}
if ([string] $settings.pythonEnvironment.status -cne 'ready') {
    throw "The repository documentation venv is not ready. Run 'dev.cmd setup'."
}

$requirementsSha256 = Get-Sha256 -Path $requirementsPath
$environmentStamp = Get-Content -LiteralPath $environmentStampPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ([string] $settings.pythonEnvironment.requirementsSha256 -cne $requirementsSha256 -or
    [string] $environmentStamp.schema -cne 'goniegonie.documentation-python-environment.v1' -or
    [string] $environmentStamp.requirementsSha256 -cne $requirementsSha256 -or
    [string] $environmentStamp.pythonVersion -cne '3.12.7' -or
    [string] $environmentStamp.oodocsVersion -cne '1.3.0' -or
    [string] $environmentStamp.venvExecutable -cne [string] $settings.pythonEnvironment.executable -or
    [string] $environmentStamp.baseExecutable -cne [string] $settings.pythonEnvironment.baseExecutable) {
    throw "The documentation venv does not match the current hash lock. Run 'dev.cmd setup' again."
}

$dotnetExecutable = [System.IO.Path]::GetFullPath([string] $settings.dotnet.executable)
$pythonExecutable = [System.IO.Path]::GetFullPath([string] $settings.pythonEnvironment.executable)
$expectedPythonExecutable = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot '.tools\venv\Scripts\python.exe'))
if (-not $pythonExecutable.Equals(
    $expectedPythonExecutable,
    [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Local settings do not select the repository documentation venv. Run 'dev.cmd setup' again."
}
$dotnetExecutable = Resolve-ExecutablePathWithRepositorySafety `
    -RepositoryRoot $repositoryRoot `
    -ExecutablePath $dotnetExecutable
$pythonExecutable = Resolve-ExecutablePathWithRepositorySafety `
    -RepositoryRoot $repositoryRoot `
    -ExecutablePath $pythonExecutable
foreach ($executable in @($dotnetExecutable, $pythonExecutable)) {
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Configured executable is missing: '$executable'. Run 'dev.cmd setup' again."
    }
}

$expectedSdk = [string] (
    (Get-Content -LiteralPath (Join-Path $repositoryRoot 'global.json') -Raw -Encoding UTF8 |
        ConvertFrom-Json).sdk.version)
if ([string] $settings.dotnet.sdkVersion -cne $expectedSdk) {
    throw "Documentation requires .NET SDK $expectedSdk. Run 'dev.cmd setup' again."
}
if ([string] $settings.pythonEnvironment.version -cne '3.12.7' -or
    [string] $settings.pythonEnvironment.oodocsVersion -cne '1.3.0') {
    throw "Documentation requires repository Python 3.12.7 with OODocs 1.3.0. Run 'dev.cmd setup' again."
}

if ($WhatIfPreference) {
    Write-Host "What if: verify the hash-locked documentation venv '$pythonExecutable'."
    Write-Host 'What if: verify the public/development documentation boundary, links, and Food4Rhino fields.'
    Write-Host "What if: restore the component catalog project graph in NuGet locked mode."
    Write-Host "What if: build the runtime component catalog project '$catalogProjectPath'."
    Write-Host "What if: extract and compare Rhino 7/net48 plus Rhino 8/net7.0-windows and net8.0-windows component catalogs."
    Write-Host "What if: generate the exhaustive In/Out reference and render '$safeOutputPath' with OODocs."
    return
}
if (-not $PSCmdlet.ShouldProcess(
    $safeOutputPath,
    'Reflect every public Dragon component and replace the generated user-guide PDF')) {
    return
}

Set-RepositoryBuildEnvironment `
    -RepositoryRoot $repositoryRoot `
    -DotNetExecutable $dotnetExecutable

Ensure-Directory -Path $safeWorkRoot
Ensure-Directory -Path $logsRoot
Ensure-Directory -Path $outputDirectory
foreach ($path in @($safeWorkRoot, $outputDirectory, $referenceDirectory)) {
    Assert-NoReparsePoints -Path $path -AnchorPath $repositoryRoot
}

Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList @(
        '-I', '-B', '-X', 'utf8',
        $environmentVerifierPath,
        '--requirements', $requirementsPath,
        '--expected-python', '3.12.7',
        '--expected-oodocs', '1.3.0') `
    -LogPath (Join-Path $logsRoot 'verify-environment.log') `
    -FailureMessage 'The repository documentation environment is not reproducible'

Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList @(
        '-I', '-B', '-X', 'utf8',
        $sourceVerifierPath,
        '--repo-root', $repositoryRoot) `
    -LogPath (Join-Path $logsRoot 'verify-documentation-sources.log') `
    -FailureMessage 'The documentation hierarchy, links, or Food4Rhino metadata is invalid'

Invoke-WithTrackedPackageLockNormalization `
    -RepositoryRoot $repositoryRoot `
    -Action {
        Invoke-LoggedNativeCommand `
            -FilePath $dotnetExecutable `
            -ArgumentList @(
                'restore', $catalogProjectPath,
                '--locked-mode',
                '--configfile', $nugetConfigPath,
                '--packages', $nugetPackagesPath,
                '--nologo') `
            -LogPath (Join-Path $logsRoot 'restore-component-catalog.log') `
            -FailureMessage 'Restoring the locked component catalog project graph failed'
    }

Invoke-LoggedNativeCommand `
    -FilePath $dotnetExecutable `
    -ArgumentList @(
        'build', $catalogProjectPath,
        '--configuration', 'Release',
        '--no-restore',
        '--nologo') `
    -LogPath (Join-Path $logsRoot 'build-component-catalog.log') `
    -FailureMessage "Building the component catalog and current Grasshopper assemblies failed; run 'dev.cmd setup' if restore assets are missing"
foreach ($catalogExecutable in @($catalogNet8AssemblyPath, $catalogNet48ExecutablePath)) {
    if (-not (Test-Path -LiteralPath $catalogExecutable -PathType Leaf)) {
        throw "The component catalog executable is missing: '$catalogExecutable'."
    }
}

Invoke-LoggedNativeCommand `
    -FilePath $dotnetExecutable `
    -ArgumentList @(
        'exec', $catalogNet8AssemblyPath,
        '--repository-root', $repositoryRoot,
        '--configuration', 'Release',
        '--framework', 'net8.0-windows',
        '--output', $catalogNet8Path) `
    -LogPath (Join-Path $logsRoot 'extract-component-catalog-net8.log') `
    -FailureMessage 'Extracting the Rhino 8 Grasshopper runtime catalog failed'

# Run the catalog host itself on the pinned .NET 8 runtime while loading the
# freshly built net7 plugin payload. This validates the public contract of the
# Rhino 8.0-8.19 binaries without installing a second command-line runtime.
Invoke-LoggedNativeCommand `
    -FilePath $dotnetExecutable `
    -ArgumentList @(
        'exec', $catalogNet8AssemblyPath,
        '--repository-root', $repositoryRoot,
        '--configuration', 'Release',
        '--framework', 'net7.0-windows',
        '--output', $catalogNet7Path) `
    -LogPath (Join-Path $logsRoot 'extract-component-catalog-net7.log') `
    -FailureMessage 'Extracting the Rhino 8 .NET 7 Grasshopper runtime catalog failed'

Invoke-LoggedNativeCommand `
    -FilePath $catalogNet48ExecutablePath `
    -ArgumentList @(
        '--repository-root', $repositoryRoot,
        '--configuration', 'Release',
        '--framework', 'net48',
        '--output', $catalogNet48Path) `
    -LogPath (Join-Path $logsRoot 'extract-component-catalog-net48.log') `
    -FailureMessage 'Extracting the Rhino 7 Grasshopper runtime catalog failed'

Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList @(
        '-I', '-B', '-X', 'utf8',
        $guideBuilderPath,
        '--repo-root', $repositoryRoot,
        '--catalog', $catalogNet8Path,
        '--compatibility-catalog', $catalogNet7Path,
        '--compatibility-catalog', $catalogNet48Path,
        '--output', $safeOutputPath) `
    -LogPath (Join-Path $logsRoot 'build-user-guide.log') `
    -FailureMessage 'Generating the exhaustive reference and OODocs PDF failed'

Assert-NoReparsePoints -Path $outputDirectory -AnchorPath $repositoryRoot
if (-not (Test-Path -LiteralPath $safeOutputPath -PathType Leaf)) {
    throw "OODocs did not create the expected PDF: '$safeOutputPath'."
}
$pdf = Get-Item -LiteralPath $safeOutputPath
if ($pdf.Length -lt 10kb) {
    throw "The generated PDF is unexpectedly small ($($pdf.Length) bytes): '$safeOutputPath'."
}
$stream = [System.IO.File]::OpenRead($safeOutputPath)
try {
    $signatureBytes = New-Object byte[] 5
    if ($stream.Read($signatureBytes, 0, 5) -ne 5 -or
        [System.Text.Encoding]::ASCII.GetString($signatureBytes) -cne '%PDF-') {
        throw "The generated documentation does not have a PDF signature: '$safeOutputPath'."
    }
}
finally {
    $stream.Dispose()
}

Write-Host "Documentation PDF: $safeOutputPath"
Write-Host "Runtime catalogs: $catalogNet48Path, $catalogNet7Path, and $catalogNet8Path (disposable)"

#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [string] $OutputPath,

    [string] $Food4RhinoOutputPath
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
$food4RhinoBuilderPath = Join-Path $repositoryRoot 'tools\documentation\build_food4rhino_metadata.py'
$food4RhinoSourcePath = Join-Path $repositoryRoot 'docs\development\publishing\food4rhino.md'
$guideMetadataPath = Join-Path $repositoryRoot 'tools\documentation\component-guides.json'
$packageSpecPath = Join-Path $repositoryRoot 'packaging\package-spec.json'
$catalogProjectPath = Join-Path $repositoryRoot 'tools\component-catalog\GonieGonie.Dragons.ComponentCatalog.csproj'
$catalogNet8AssemblyPath = Join-Path $repositoryRoot 'temp\build\bin\GonieGonie.Dragons.ComponentCatalog\Release\net8.0-windows\GonieGonie.Dragons.ComponentCatalog.dll'
$catalogNet48ExecutablePath = Join-Path $repositoryRoot 'temp\build\bin\GonieGonie.Dragons.ComponentCatalog\Release\net48\GonieGonie.Dragons.ComponentCatalog.exe'
$workRoot = Join-Path $repositoryRoot 'temp\documentation'
$catalogNet8Path = Join-Path $workRoot 'component-catalog.net8.0-windows.json'
$catalogNet7Path = Join-Path $workRoot 'component-catalog.net7.0-windows.json'
$catalogNet48Path = Join-Path $workRoot 'component-catalog.net48.json'
$logsRoot = Join-Path $workRoot 'logs'
$documentationOutputRoot = Join-Path $repositoryRoot 'artifacts\documentation'

if (-not (Test-Path -LiteralPath $packageSpecPath -PathType Leaf)) {
    throw "Package specification is missing: '$packageSpecPath'."
}
$packageSpec = Get-Content -LiteralPath $packageSpecPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ([string] $packageSpec.schema -cne 'goniegonie.dragons-grasshopper.package-spec.v3' -or
    [string] $packageSpec.version -cne '0.1.0') {
    throw 'Documentation requires the deliberate first-release package version 0.1.0.'
}
$releaseVersion = [string] $packageSpec.version
$defaultOutputPath = Join-Path $documentationOutputRoot (
    "Dragons-Grasshopper-User-Guide-$releaseVersion.pdf")
$defaultFood4RhinoOutputPath = Join-Path $documentationOutputRoot (
    "Dragons-Grasshopper-Food4Rhino-Metadata-$releaseVersion.pdf")

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = $defaultOutputPath
}
if ([string]::IsNullOrWhiteSpace($Food4RhinoOutputPath)) {
    $Food4RhinoOutputPath = $defaultFood4RhinoOutputPath
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$Food4RhinoOutputPath = [System.IO.Path]::GetFullPath($Food4RhinoOutputPath)
foreach ($candidate in @($OutputPath, $Food4RhinoOutputPath)) {
    if (-not [System.IO.Path]::GetExtension($candidate).Equals(
        '.pdf',
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Documentation output must be a PDF: '$candidate'."
    }
}
$safeOutputPath = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $OutputPath `
    -AllowedTopLevelNames @('artifacts')
$safeFood4RhinoOutputPath = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $Food4RhinoOutputPath `
    -AllowedTopLevelNames @('artifacts')
$safeWorkRoot = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $workRoot `
    -AllowedTopLevelNames @('temp')
$outputDirectory = Split-Path -Parent $safeOutputPath
$food4RhinoOutputDirectory = Split-Path -Parent $safeFood4RhinoOutputPath
$referenceDirectory = Join-Path $repositoryRoot 'docs\user\user-guide'

# Lexical containment is not enough for a write workflow: a junction beneath
# temp, artifacts, or docs could redirect a later replace outside the checkout.
$initialDocumentationWritePaths = @(
    $safeWorkRoot,
    $outputDirectory,
    $food4RhinoOutputDirectory,
    $referenceDirectory) | Select-Object -Unique
foreach ($path in $initialDocumentationWritePaths) {
    Assert-NoReparsePoints -Path $path -AnchorPath $repositoryRoot
}

foreach ($requiredFile in @(
    $settingsPath,
    $environmentStampPath,
    $requirementsPath,
    $environmentVerifierPath,
    $sourceVerifierPath,
    $guideBuilderPath,
    $food4RhinoBuilderPath,
    $food4RhinoSourcePath,
    $guideMetadataPath,
    $packageSpecPath,
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
    Write-Host "What if: render the Food4Rhino publishing worksheet '$safeFood4RhinoOutputPath' with OODocs."
    return
}
if (-not $PSCmdlet.ShouldProcess(
    "$safeOutputPath; $safeFood4RhinoOutputPath",
    'Reflect every public Dragon component and replace both generated release PDFs')) {
    return
}

Set-RepositoryBuildEnvironment `
    -RepositoryRoot $repositoryRoot `
    -DotNetExecutable $dotnetExecutable

Ensure-Directory -Path $safeWorkRoot
Ensure-Directory -Path $logsRoot
Ensure-Directory -Path $outputDirectory
Ensure-Directory -Path $food4RhinoOutputDirectory
$documentationWritePaths = @(
    $safeWorkRoot,
    $outputDirectory,
    $food4RhinoOutputDirectory,
    $referenceDirectory) | Select-Object -Unique
foreach ($path in $documentationWritePaths) {
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

Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList @(
        '-I', '-B', '-X', 'utf8',
        $food4RhinoBuilderPath,
        '--repo-root', $repositoryRoot,
        '--output', $safeFood4RhinoOutputPath) `
    -LogPath (Join-Path $logsRoot 'build-food4rhino-metadata.log') `
    -FailureMessage 'Generating the Food4Rhino OODocs metadata PDF failed'

foreach ($directory in @($outputDirectory, $food4RhinoOutputDirectory) | Select-Object -Unique) {
    Assert-NoReparsePoints -Path $directory -AnchorPath $repositoryRoot
}
foreach ($pdfPath in @($safeOutputPath, $safeFood4RhinoOutputPath)) {
    if (-not (Test-Path -LiteralPath $pdfPath -PathType Leaf)) {
        throw "OODocs did not create the expected PDF: '$pdfPath'."
    }
    $pdf = Get-Item -LiteralPath $pdfPath
    if ($pdf.Length -lt 10kb) {
        throw "The generated PDF is unexpectedly small ($($pdf.Length) bytes): '$pdfPath'."
    }
    $stream = [System.IO.File]::OpenRead($pdfPath)
    try {
        $signatureBytes = New-Object byte[] 5
        if ($stream.Read($signatureBytes, 0, 5) -ne 5 -or
            [System.Text.Encoding]::ASCII.GetString($signatureBytes) -cne '%PDF-') {
            throw "The generated documentation does not have a PDF signature: '$pdfPath'."
        }
    }
    finally {
        $stream.Dispose()
    }
}

Write-Host "User guide PDF: $safeOutputPath"
Write-Host "Food4Rhino metadata PDF: $safeFood4RhinoOutputPath"
Write-Host "Runtime catalogs: $catalogNet48Path, $catalogNet7Path, and $catalogNet8Path (disposable)"

#requires -Version 5.1

[CmdletBinding()]
param(
    [string] $Case,
    [string] $OutputDirectory,
    [switch] $SkipEnergyPlus,
    [switch] $SkipReferencePreparation,
    [switch] $AllowDifferences,
    [switch] $NoRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
$settingsPath = Join-Path $repositoryRoot '.config\local.settings.json'
$manifestPath = Join-Path $repositoryRoot 'fixtures\compatibility\cases.json'
$bootstrapPath = Join-Path $repositoryRoot 'tools\python-reference\bootstrap_reference.py'
$pythonEnginePath = Join-Path $repositoryRoot 'tools\compatibility-runner\python_engine.py'
$reporterPath = Join-Path $repositoryRoot 'tools\compatibility-runner\compare_outputs.py'
$csharpProject = Join-Path $repositoryRoot 'tools\compatibility-runner\GonieGonie.CompatibilityRunner.csproj'
$runtimeManifestPath = Join-Path $repositoryRoot 'runtime\manifest.template.json'
$compatibilityExceptionsPath = Join-Path $repositoryRoot 'upstream\compatibility-exceptions.yml'
$dependencyRoot = Join-Path $repositoryRoot '.tools\python-reference\3.12.7\site-packages'
$upstreamRoot = Join-Path $repositoryRoot 'temp\reference\upstream\eplussimple'
$logsRoot = Join-Path $repositoryRoot 'temp\compatibility\logs'
$artifactReport = Join-Path $repositoryRoot 'artifacts\reports\engineering-compatibility.json'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'temp\compatibility\current'
}
$outputRoot = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $OutputDirectory `
    -AllowedTopLevelNames @('temp')
$pythonOutput = Join-Path $outputRoot 'python'
$csharpOutput = Join-Path $outputRoot 'csharp'

foreach ($requiredFile in @(
    $settingsPath,
    $manifestPath,
    $bootstrapPath,
    $pythonEnginePath,
    $reporterPath,
    $csharpProject,
    $runtimeManifestPath,
    $compatibilityExceptionsPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required compatibility input is missing: '$requiredFile'."
    }
}

$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
if ([string] $settings.pythonOracle.status -ne 'ready') {
    throw "Python 3.12.7 is not ready. Run 'dev.cmd setup'."
}
if ([string] $settings.energyPlus.status -ne 'ready') {
    throw "Pinned EnergyPlus 24.2.0 is not ready. Run 'dev.cmd setup -InstallEnergyPlus'."
}
if ([string] $settings.dotnet.status -ne 'ready') {
    throw "Pinned .NET SDK is not ready. Run 'dev.cmd setup'."
}

$pythonExecutable = [string] $settings.pythonOracle.executable
$runtimeRoot = [string] $settings.energyPlus.root
$dotnetExecutable = [string] $settings.dotnet.executable
$iddPath = Join-Path $runtimeRoot 'Energy+.idd'

if (-not (Test-Path -LiteralPath $iddPath -PathType Leaf)) {
    throw "Pinned EnergyPlus IDD is missing: '$iddPath'."
}

if (-not $SkipReferencePreparation) {
    $powerShell = Join-Path $PSHOME 'powershell.exe'
    & $powerShell `
        -NoLogo `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'reference.ps1') `
        -Mode Verify
    if ($LASTEXITCODE -ne 0) {
        throw "Pinned Python reference preparation failed with exit code $LASTEXITCODE."
    }
}

foreach ($requiredDirectory in @($dependencyRoot, $upstreamRoot, $runtimeRoot)) {
    if (-not (Test-Path -LiteralPath $requiredDirectory -PathType Container)) {
        throw "Required compatibility directory is missing: '$requiredDirectory'."
    }
}

if (Test-Path -LiteralPath $outputRoot -PathType Container) {
    Assert-NoReparsePoints -Path $outputRoot -AnchorPath $repositoryRoot
    foreach ($item in @(Get-ChildItem -LiteralPath $outputRoot -Force)) {
        $safeItem = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path $item.FullName `
            -AllowedTopLevelNames @('temp')
        Remove-Item -LiteralPath $safeItem -Recurse -Force
    }
}
Ensure-Directory -Path $outputRoot
Ensure-Directory -Path $pythonOutput
Ensure-Directory -Path $csharpOutput
Ensure-Directory -Path $logsRoot
Ensure-Directory -Path (Split-Path -Parent $artifactReport)

Set-RepositoryBuildEnvironment `
    -RepositoryRoot $repositoryRoot `
    -DotNetExecutable $dotnetExecutable
$dotnetArguments = @(
    'run',
    '--project', $csharpProject,
    '--configuration', 'Release')
if ($NoRestore) {
    $dotnetArguments += '--no-restore'
}
$dotnetArguments += @(
    '--',
    '--repository-root', $repositoryRoot,
    '--runtime-root', $runtimeRoot,
    '--manifest', $manifestPath,
    '--output', $csharpOutput)
if (-not [string]::IsNullOrWhiteSpace($Case)) {
    $dotnetArguments += @('--case', $Case)
}
if ($SkipEnergyPlus) {
    $dotnetArguments += '--skip-energyplus'
}
Invoke-LoggedNativeCommand `
    -FilePath $dotnetExecutable `
    -ArgumentList $dotnetArguments `
    -LogPath (Join-Path $logsRoot 'csharp-engine.log') `
    -FailureMessage 'The C# compatibility engine failed'

$env:PYTHONHASHSEED = '0'
$env:PYTHONUTF8 = '1'
$env:PYTHONDONTWRITEBYTECODE = '1'
$pythonArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', (Join-Path $upstreamRoot 'src'),
    '--generator', $pythonEnginePath,
    '--',
    '--repository-root', $repositoryRoot,
    '--upstream-root', $upstreamRoot,
    '--runtime-root', $runtimeRoot,
    '--manifest', $manifestPath,
    '--output', $pythonOutput,
    '--csharp-output', $csharpOutput)
if (-not [string]::IsNullOrWhiteSpace($Case)) {
    $pythonArguments += @('--case', $Case)
}
if ($SkipEnergyPlus) {
    $pythonArguments += '--skip-energyplus'
}
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $pythonArguments `
    -LogPath (Join-Path $logsRoot 'python-engine.log') `
    -FailureMessage 'The Python compatibility engine failed'

$reportArguments = @(
    '-X', 'utf8',
    $reporterPath,
    '--manifest', $manifestPath,
    '--python-output', $pythonOutput,
    '--csharp-output', $csharpOutput,
    '--idd', $iddPath,
    '--runtime-manifest', $runtimeManifestPath,
    '--compatibility-exceptions', $compatibilityExceptionsPath,
    '--report', $artifactReport)
if (-not [string]::IsNullOrWhiteSpace($Case)) {
    $reportArguments += @('--case', $Case)
}
if ($SkipEnergyPlus) {
    $reportArguments += '--skip-energyplus'
}
if ($AllowDifferences) {
    $reportArguments += '--allow-differences'
}
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $reportArguments `
    -LogPath (Join-Path $logsRoot 'comparison.log') `
    -FailureMessage 'Engineering compatibility differences were found'

Write-Host "Compatibility output: $outputRoot"
Write-Host "Engineering report: $artifactReport"

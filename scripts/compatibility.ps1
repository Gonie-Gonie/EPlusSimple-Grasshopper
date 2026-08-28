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

function Get-EngineeringSha256([string] $Path) {
    return 'sha256:' + (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-EngineeringGitState {
    $gitCommand = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($null -eq $gitCommand) { $gitCommand = Get-Command git -ErrorAction SilentlyContinue }
    if ($null -eq $gitCommand) { throw 'Git is required to bind engineering compatibility provenance.' }
    $head = (& $gitCommand.Source -C $repositoryRoot rev-parse HEAD 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -cnotmatch '^[0-9a-f]{40}$') {
        throw 'Unable to bind engineering compatibility to the port HEAD.'
    }
    $status = @(& $gitCommand.Source -C $repositoryRoot status --porcelain=v1 --untracked-files=all 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to determine the port worktree state.' }
    return [pscustomobject] [ordered] @{ commit = $head; dirty = $status.Count -ne 0 }
}

function Get-EngineeringSourceSet {
    $roots = @(
        'src/Shared/GonieGonie.BuildingEnergy.Contracts',
        'src/Shared/GonieGonie.EnergyPlus.Runtime',
        'src/InvisibleDragon/GonieGonie.InvisibleDragon.Core',
        'src/SimpleDragon/GonieGonie.SimpleDragon.Core',
        'tools/compatibility-runner'
    )
    $files = @($roots | ForEach-Object {
        Get-ChildItem -LiteralPath (Join-Path $repositoryRoot $_) -File -Recurse |
            Where-Object { $_.Extension -in @('.cs', '.csproj') }
    } | Sort-Object FullName -Unique)
    if ($files.Count -lt 5) { throw 'Engineering production source set is unexpectedly incomplete.' }
    $entries = @($files | ForEach-Object {
        $relative = $_.FullName.Substring($repositoryRoot.Length + 1).Replace('\', '/')
        [pscustomobject] [ordered] @{
            path = $relative
            bytes = [long] $_.Length
            sha256 = Get-EngineeringSha256 -Path $_.FullName
        }
    })
    $lines = @($entries | ForEach-Object { "$($_.sha256)  $($_.bytes)  $($_.path)" })
    $encoded = [System.Text.UTF8Encoding]::new($false).GetBytes(($lines -join "`n") + "`n")
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $aggregate = 'sha256:' + ([BitConverter]::ToString($sha.ComputeHash($encoded)) -replace '-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    return [pscustomobject] [ordered] @{ file_count = $entries.Count; sha256 = $aggregate; files = $entries }
}

function Get-EngineeringBinarySet {
    $directory = Join-Path $repositoryRoot 'temp\build\bin\GonieGonie.CompatibilityRunner\Release\net8.0-windows'
    $names = @(
        'GonieGonie.CompatibilityRunner.dll',
        'GonieGonie.BuildingEnergy.Contracts.dll',
        'GonieGonie.EnergyPlus.Runtime.dll',
        'GonieGonie.InvisibleDragon.Core.dll',
        'GonieGonie.SimpleDragon.Core.dll'
    )
    $entries = @($names | ForEach-Object {
        $path = Join-Path $directory $_
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Executed engineering binary is missing: '$path'." }
        $item = Get-Item -LiteralPath $path
        $identity = [Reflection.AssemblyName]::GetAssemblyName($path)
        [pscustomobject] [ordered] @{
            path = $item.FullName.Substring($repositoryRoot.Length + 1).Replace('\', '/')
            bytes = [long] $item.Length
            sha256 = Get-EngineeringSha256 -Path $path
            assembly_name = $identity.Name
            assembly_version = $identity.Version.ToString()
            target_framework = 'net8.0-windows'
        }
    })
    return [pscustomobject] [ordered] @{
        target_framework = 'net8.0-windows'
        configuration = 'Release'
        gha_executed = $false
        gha_reason = 'The engineering runner executes production Core and Runtime assemblies directly; no Grasshopper host participates.'
        files = $entries
    }
}

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

$expectedCaseIds = @(
    'ashrae-140-modified',
    'two-zone-one-sided-adjacency-shared-hp',
    'screw-chiller-closed-two-speed-fcu',
    'packaged-erv-pv-openings',
    'packaged-erv-pv-openings--tampa',
    'packaged-erv-pv-openings--golden',
    'packaged-erv-pv-openings--san-francisco',
    'geothermal-heat-pump-ahu',
    'boiler-heating-fuel-shared-matrix',
    'absorption-default-explicit-electric-radiant',
    'district-shared-fcu-radiator-radiant-dhw'
) | Sort-Object
$expectedStages = @(
    'grm_cross_read', 'authoring_idf', 'expanded_idf',
    'energyplus', 'grr', 'warnings'
) | Sort-Object
$compatibilityManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifestCases = @($compatibilityManifest.cases)
$manifestCaseIds = @($manifestCases | ForEach-Object { [string] $_.id } | Sort-Object)
if ([string] $compatibilityManifest.schema -cne 'goniegonie.dragons.compatibility-cases.v1' -or
    $manifestCases.Count -ne 11 -or
    [int] ($manifestCases | ForEach-Object { @($_.stages).Count } | Measure-Object -Sum).Sum -ne 66 -or
    @($manifestCaseIds | Select-Object -Unique).Count -ne 11 -or
    @(Compare-Object -ReferenceObject $expectedCaseIds -DifferenceObject $manifestCaseIds).Count -ne 0) {
    throw 'Compatibility manifest must declare the exact eleven-case climate matrix.'
}
foreach ($manifestCase in $manifestCases) {
    $caseStages = @($manifestCase.stages | ForEach-Object { [string] $_ } | Sort-Object)
    if ($caseStages.Count -ne 6 -or
        @(Compare-Object -ReferenceObject $expectedStages -DifferenceObject $caseStages).Count -ne 0 -or
        [string] $manifestCase.weather_sha256 -cnotmatch '^[0-9a-f]{64}$' -or
        [string]::IsNullOrWhiteSpace([string] $manifestCase.weather_header) -or
        -not ([string] $manifestCase.weather_header).StartsWith('LOCATION,', [StringComparison]::Ordinal)) {
        throw "Compatibility case '$($manifestCase.id)' does not bind six stages and a pinned EPW hash/header receipt."
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

$resolvedRuntimeRoot = [IO.Path]::GetFullPath($runtimeRoot).TrimEnd('\') + '\'
foreach ($manifestCase in $manifestCases) {
    $weatherPath = [IO.Path]::GetFullPath((Join-Path $runtimeRoot ([string] $manifestCase.weather)))
    if (-not $weatherPath.StartsWith($resolvedRuntimeRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $weatherPath -PathType Leaf)) {
        throw "Compatibility case '$($manifestCase.id)' has a missing or unsafe EPW path."
    }
    $weatherHash = (Get-FileHash -LiteralPath $weatherPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $weatherHeader = Get-Content -LiteralPath $weatherPath -TotalCount 1
    if ($weatherHash -cne [string] $manifestCase.weather_sha256 -or
        [string] $weatherHeader -cne [string] $manifestCase.weather_header) {
        throw "Compatibility case '$($manifestCase.id)' EPW hash/header receipt drifted."
    }
}

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

# Bind the semantic comparison to the exact port sources and production
# binaries that produced it. This is additive to the v1 report schema so older
# readers can continue consuming the established comparison fields.
$engineeringReport = Get-Content -LiteralPath $artifactReport -Raw | ConvertFrom-Json
$provenance = [pscustomobject] [ordered] @{
    schema = 'goniegonie.dragons.engineering-port-provenance.v1'
    git = Get-EngineeringGitState
    production_source_set = Get-EngineeringSourceSet
    executed_binaries = Get-EngineeringBinarySet
}
$engineeringReport | Add-Member -NotePropertyName port_provenance -NotePropertyValue $provenance -Force
[System.IO.File]::WriteAllText(
    $artifactReport,
    ($engineeringReport | ConvertTo-Json -Depth 100) + "`n",
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Compatibility output: $outputRoot"
Write-Host "Engineering report: $artifactReport"

#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [ValidateSet('Generate', 'Verify')]
    [string] $Mode = 'Generate',

    [string] $OutputDirectory,

    [string] $BaselineDirectory,

    [string] $UpstreamPath,

    [switch] $RefreshDependencies,

    [switch] $UpdateBaseline
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
$settingsPath = Join-Path $repositoryRoot '.config\local.settings.json'
$lockPath = Join-Path $repositoryRoot 'upstream\upstream.lock.json'
$requirementsPath = Join-Path $repositoryRoot 'tools\python-reference\requirements.lock.txt'
$bootstrapPath = Join-Path $repositoryRoot 'tools\python-reference\bootstrap_reference.py'
$generatorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_reference.py'
$profileGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_usage_profile_schedule_oracle.py'
$iddGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_idd_schema_oracle.py'
$tempRoot = Join-Path $repositoryRoot 'temp'
$referenceTempRoot = Join-Path $tempRoot 'reference'
$logsRoot = Join-Path $referenceTempRoot 'logs'
$dependencyParent = Join-Path $repositoryRoot '.tools\python-reference\3.12.7'
$dependencyRoot = Join-Path $dependencyParent 'site-packages'
$dependencyStamp = Join-Path $dependencyParent 'installed.json'
$pipCache = Join-Path $referenceTempRoot 'pip-cache'
$pipWheel = Join-Path $repositoryRoot '.tools\python-reference\bootstrap\pip-24.3.1-py3-none-any.whl'
$pipWheelUri = 'https://files.pythonhosted.org/packages/ef/7d/500c9ad20238fcfcb4cb9243eede163594d7020ce87bd9610c9e02771876/pip-24.3.1-py3-none-any.whl'
$pipWheelSha256 = '3790624780082365f47549d032f3770eeb2b1e8bd1f7b2e02dace1afa361b4ed'
$requiredPythonVersion = '3.12.7'
$requiredEnergyPlusVersion = '24.2.0'
$requiredEnergyPlusBuild = '94a887817b'
$requiredEnergyPlusIddSha256 = '3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2'
$requiredEnergyPlusEpJsonSchemaSha256 = 'aefb16d63495d170468ecab3c935f1aeb68eb07c6551403dd11cbba61cb136fa'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $referenceTempRoot 'python-output'
}
if ([string]::IsNullOrWhiteSpace($BaselineDirectory)) {
    $BaselineDirectory = Join-Path $repositoryRoot 'fixtures\reference\python-0.7.0'
}

$outputRoot = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $OutputDirectory `
    -AllowedTopLevelNames @('temp')
$baselineRoot = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $BaselineDirectory `
    -AllowedTopLevelNames @('fixtures')
$dependencyParent = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $dependencyParent `
    -AllowedTopLevelNames @('.tools')

if ($Mode -eq 'Verify' -and $UpdateBaseline) {
    throw '-UpdateBaseline cannot be combined with -Mode Verify.'
}

foreach ($requiredFile in @($settingsPath, $lockPath, $requirementsPath, $bootstrapPath, $generatorPath, $profileGeneratorPath, $iddGeneratorPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required reference-oracle input is missing: '$requiredFile'. Run 'dev.cmd setup' if local.settings.json is absent."
    }
}

$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$pythonSettings = $settings.PSObject.Properties['pythonOracle']
if ($null -eq $pythonSettings -or [string] $pythonSettings.Value.status -ne 'ready') {
    throw "The exact Python oracle is not configured. Run 'dev.cmd setup' without -SkipPythonInstall."
}

$pythonExecutable = [string] $pythonSettings.Value.executable
if (-not (Test-Path -LiteralPath $pythonExecutable -PathType Leaf)) {
    throw "The setup-selected Python executable no longer exists: '$pythonExecutable'. Re-run 'dev.cmd setup'."
}

$pythonIdentity = @(& $pythonExecutable -c "import sys; print('%d.%d.%d' % sys.version_info[:3])" 2>$null)
if ($LASTEXITCODE -ne 0 -or $pythonIdentity.Count -eq 0 -or [string] $pythonIdentity[-1] -ne $requiredPythonVersion) {
    $reported = if ($pythonIdentity.Count -gt 0) { [string] $pythonIdentity[-1] } else { '<none>' }
    throw "Python $requiredPythonVersion is required for the reference oracle; configured interpreter reported '$reported'."
}

$energyPlusSettings = $settings.PSObject.Properties['energyPlus']
if ($null -eq $energyPlusSettings -or [string] $energyPlusSettings.Value.status -ne 'ready') {
    throw "The EnergyPlus IDD oracle is not configured. Run 'dev.cmd setup -InstallEnergyPlus'."
}

$energyPlusIddPath = [string] $energyPlusSettings.Value.idd
if (-not (Test-Path -LiteralPath $energyPlusIddPath -PathType Leaf)) {
    throw "The setup-selected EnergyPlus IDD no longer exists: '$energyPlusIddPath'. Re-run 'dev.cmd setup'."
}
$energyPlusEpJsonSchemaPath = [string] $energyPlusSettings.Value.epJsonSchema
if (-not (Test-Path -LiteralPath $energyPlusEpJsonSchemaPath -PathType Leaf)) {
    throw "The setup-selected official EnergyPlus epJSON schema no longer exists: '$energyPlusEpJsonSchemaPath'. Re-run 'dev.cmd setup'."
}
if ([string] $energyPlusSettings.Value.version -ne $requiredEnergyPlusVersion -or
    [string] $energyPlusSettings.Value.build -ne $requiredEnergyPlusBuild) {
    throw "EnergyPlus $requiredEnergyPlusVersion build $requiredEnergyPlusBuild is required for the IDD oracle."
}
$actualEnergyPlusIddSha256 = Get-Sha256 -Path $energyPlusIddPath
if ($actualEnergyPlusIddSha256 -ne $requiredEnergyPlusIddSha256) {
    throw "EnergyPlus IDD hash mismatch. Expected '$requiredEnergyPlusIddSha256', found '$actualEnergyPlusIddSha256'."
}
$actualEnergyPlusEpJsonSchemaSha256 = Get-Sha256 -Path $energyPlusEpJsonSchemaPath
if ($actualEnergyPlusEpJsonSchemaSha256 -ne $requiredEnergyPlusEpJsonSchemaSha256) {
    throw "Official EnergyPlus epJSON schema hash mismatch. Expected '$requiredEnergyPlusEpJsonSchemaSha256', found '$actualEnergyPlusEpJsonSchemaSha256'."
}

$upstreamLock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$upstreamRepository = [string] $upstreamLock.repository
$upstreamCommit = [string] $upstreamLock.commit
$manageUpstream = [string]::IsNullOrWhiteSpace($UpstreamPath)
if ($manageUpstream) {
    $UpstreamPath = Join-Path $referenceTempRoot 'upstream\eplussimple'
    $UpstreamPath = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $UpstreamPath `
        -AllowedTopLevelNames @('temp')
}
else {
    $UpstreamPath = [System.IO.Path]::GetFullPath($UpstreamPath)
}

function Invoke-CapturedNativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [string[]] $ArgumentList = @(),

        [Parameter(Mandatory = $true)]
        [string] $FailureMessage
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $FilePath @ArgumentList 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "$FailureMessage (exit code $exitCode): $($output -join [Environment]::NewLine)"
    }

    return @($output)
}

function Reset-ReferenceOwnedTree {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string[]] $AllowedTopLevelNames
    )

    $safePath = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $Path `
        -AllowedTopLevelNames $AllowedTopLevelNames
    if (-not (Test-Path -LiteralPath $safePath)) {
        return
    }

    Assert-NoReparsePoints -Path $safePath
    if ($WhatIfPreference) {
        Write-Host "What if: remove reference-owned directory '$safePath'."
        return
    }

    Remove-Item -LiteralPath $safePath -Recurse -Force
}

function Get-CanonicalRemoteUrl {
    param([Parameter(Mandatory = $true)][string] $Url)

    return $Url.Trim().TrimEnd('/').ToLowerInvariant() -replace '\.git$', ''
}

function Initialize-UpstreamCheckout {
    $gitCommand = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($null -eq $gitCommand) {
        $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    }
    if ($null -eq $gitCommand) {
        throw 'Git is required to materialize the pinned Python reference source.'
    }

    $newCheckout = $false
    if (-not (Test-Path -LiteralPath $UpstreamPath -PathType Container)) {
        if (-not $manageUpstream) {
            throw "The explicitly selected upstream checkout does not exist: '$UpstreamPath'."
        }

        Ensure-Directory -Path (Split-Path -Parent $UpstreamPath)
        if ($PSCmdlet.ShouldProcess($UpstreamPath, "Clone pinned upstream repository $upstreamRepository")) {
            Invoke-LoggedNativeCommand `
                -FilePath $gitCommand.Source `
                -ArgumentList @('clone', '--filter=blob:none', '--no-checkout', $upstreamRepository, $UpstreamPath) `
                -LogPath (Join-Path $logsRoot 'upstream-clone.log') `
                -FailureMessage 'Cloning the Python reference source failed'
            $newCheckout = $true
        }
    }

    if ($WhatIfPreference -and -not (Test-Path -LiteralPath (Join-Path $UpstreamPath '.git'))) {
        Write-Host "What if: fetch and check out upstream commit $upstreamCommit."
        return
    }

    if (-not (Test-Path -LiteralPath (Join-Path $UpstreamPath '.git') -PathType Container)) {
        throw "The upstream path is not a Git checkout: '$UpstreamPath'."
    }

    $remote = [string] (@(Invoke-CapturedNativeCommand `
        -FilePath $gitCommand.Source `
        -ArgumentList @('-C', $UpstreamPath, 'remote', 'get-url', 'origin') `
        -FailureMessage 'Reading the upstream origin failed')[-1])
    if ((Get-CanonicalRemoteUrl -Url $remote) -ne (Get-CanonicalRemoteUrl -Url $upstreamRepository)) {
        throw "The selected checkout origin '$remote' does not match the pinned repository '$upstreamRepository'."
    }

    if ($newCheckout) {
        if ($PSCmdlet.ShouldProcess($UpstreamPath, "Fetch and check out pinned upstream commit $upstreamCommit")) {
            Invoke-LoggedNativeCommand `
                -FilePath $gitCommand.Source `
                -ArgumentList @('-C', $UpstreamPath, 'fetch', '--depth', '1', 'origin', $upstreamCommit) `
                -LogPath (Join-Path $logsRoot 'upstream-fetch.log') `
                -FailureMessage 'Fetching the pinned Python reference commit failed'
            Invoke-LoggedNativeCommand `
                -FilePath $gitCommand.Source `
                -ArgumentList @('-C', $UpstreamPath, 'checkout', '--detach', $upstreamCommit) `
                -LogPath (Join-Path $logsRoot 'upstream-checkout.log') `
                -FailureMessage 'Checking out the pinned Python reference commit failed'
        }
    }
    else {
        $dirty = @(Invoke-CapturedNativeCommand `
            -FilePath $gitCommand.Source `
            -ArgumentList @('-C', $UpstreamPath, 'status', '--porcelain', '--untracked-files=normal') `
            -FailureMessage 'Checking the upstream worktree failed')
        if ($dirty.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace(($dirty -join ''))) {
            throw "The selected upstream checkout has local changes. The oracle will not overwrite them: '$UpstreamPath'."
        }
    }

    $currentCommit = [string] (@(Invoke-CapturedNativeCommand `
        -FilePath $gitCommand.Source `
        -ArgumentList @('-C', $UpstreamPath, 'rev-parse', 'HEAD') `
        -FailureMessage 'Reading the upstream commit failed')[-1])
    if (-not $newCheckout -and -not $currentCommit.Equals($upstreamCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        if (-not $manageUpstream) {
            throw "Explicit upstream checkout is at $currentCommit; expected $upstreamCommit."
        }

        if ($PSCmdlet.ShouldProcess($UpstreamPath, "Fetch and check out pinned upstream commit $upstreamCommit")) {
            Invoke-LoggedNativeCommand `
                -FilePath $gitCommand.Source `
                -ArgumentList @('-C', $UpstreamPath, 'fetch', '--depth', '1', 'origin', $upstreamCommit) `
                -LogPath (Join-Path $logsRoot 'upstream-fetch.log') `
                -FailureMessage 'Fetching the pinned Python reference commit failed'
            Invoke-LoggedNativeCommand `
                -FilePath $gitCommand.Source `
                -ArgumentList @('-C', $UpstreamPath, 'checkout', '--detach', $upstreamCommit) `
                -LogPath (Join-Path $logsRoot 'upstream-checkout.log') `
                -FailureMessage 'Checking out the pinned Python reference commit failed'
        }
    }

    if (-not $WhatIfPreference) {
        $verifiedCommit = [string] (@(Invoke-CapturedNativeCommand `
            -FilePath $gitCommand.Source `
            -ArgumentList @('-C', $UpstreamPath, 'rev-parse', 'HEAD') `
            -FailureMessage 'Verifying the upstream commit failed')[-1])
        if (-not $verifiedCommit.Equals($upstreamCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Pinned upstream checkout verification failed: expected $upstreamCommit, found $verifiedCommit."
        }
    }
}

function Install-ReferenceDependencies {
    $requirementsSha256 = Get-Sha256 -Path $requirementsPath
    $dependencyReady = $false
    if (-not $RefreshDependencies -and
        (Test-Path -LiteralPath $dependencyRoot -PathType Container) -and
        (Test-Path -LiteralPath $dependencyStamp -PathType Leaf)) {
        try {
            $stamp = Get-Content -LiteralPath $dependencyStamp -Raw | ConvertFrom-Json
            $dependencyReady = `
                ([string] $stamp.pythonVersion -eq $requiredPythonVersion) -and `
                ([string] $stamp.requirementsSha256 -eq $requirementsSha256) -and `
                ([string] $stamp.pipWheelSha256 -eq $pipWheelSha256)
        }
        catch {
            $dependencyReady = $false
        }
    }

    if ($dependencyReady) {
        Write-Host "Python reference dependencies: ready ($dependencyRoot)"
        return
    }

    $stagingRoot = Join-Path $dependencyParent 'site-packages.staging'
    Reset-ReferenceOwnedTree -Path $stagingRoot -AllowedTopLevelNames @('.tools')
    Ensure-Directory -Path $stagingRoot
    Ensure-Directory -Path (Split-Path -Parent $pipWheel)
    Ensure-Directory -Path $pipCache

    $downloadPip = -not (Test-Path -LiteralPath $pipWheel -PathType Leaf)
    if (-not $downloadPip) {
        $downloadPip = (Get-Sha256 -Path $pipWheel) -ne $pipWheelSha256
    }
    if ($downloadPip) {
        if ($PSCmdlet.ShouldProcess($pipWheel, 'Download the pinned bootstrap pip wheel')) {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -UseBasicParsing -Uri $pipWheelUri -OutFile $pipWheel
        }
    }

    if ($WhatIfPreference) {
        Write-Host "What if: verify pip wheel and install pinned requirements into '$dependencyRoot'."
        return
    }

    $actualPipHash = Get-Sha256 -Path $pipWheel
    if ($actualPipHash -ne $pipWheelSha256) {
        throw "Bootstrap pip wheel SHA-256 mismatch. Expected $pipWheelSha256; got $actualPipHash."
    }

    $pipCode = @'
import sys
sys.path.insert(0, sys.argv[1])
from pip._internal.cli.main import main
raise SystemExit(main(sys.argv[2:]))
'@
    $pipArguments = @(
        '-X', 'utf8',
        '-c', $pipCode,
        $pipWheel,
        'install',
        '--disable-pip-version-check',
        '--no-input',
        '--no-deps',
        '--requirement', $requirementsPath,
        '--target', $stagingRoot,
        '--cache-dir', $pipCache
    )
    Invoke-LoggedNativeCommand `
        -FilePath $pythonExecutable `
        -ArgumentList $pipArguments `
        -LogPath (Join-Path $logsRoot 'python-dependencies.log') `
        -FailureMessage 'Installing Python reference dependencies failed'

    Reset-ReferenceOwnedTree -Path $dependencyRoot -AllowedTopLevelNames @('.tools')
    Move-Item -LiteralPath $stagingRoot -Destination $dependencyRoot
    $stamp = [ordered] @{
        schema = 'goniegonie.python-reference.dependencies.v1'
        pythonVersion = $requiredPythonVersion
        requirementsSha256 = $requirementsSha256
        pipVersion = '24.3.1'
        pipWheelSha256 = $pipWheelSha256
    }
    Write-Utf8JsonIfChanged -InputObject $stamp -Path $dependencyStamp -Depth 4
}

function Reset-OutputDirectory {
    if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) {
        Ensure-Directory -Path $outputRoot
        return
    }

    Assert-NoReparsePoints -Path $outputRoot
    foreach ($item in @(Get-ChildItem -LiteralPath $outputRoot -Force)) {
        $safeItem = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path $item.FullName `
            -AllowedTopLevelNames @('temp')
        if ($WhatIfPreference) {
            Write-Host "What if: remove prior reference output '$safeItem'."
        }
        else {
            Remove-Item -LiteralPath $safeItem -Recurse -Force
        }
    }
}

function Get-TreeHashes {
    param([Parameter(Mandatory = $true)][string] $Root)

    $result = [ordered] @{}
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        return $result
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -File -Recurse | Sort-Object FullName)) {
        $relative = $file.FullName.Substring(([System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')).Length).TrimStart('\', '/') -replace '\\', '/'
        $result[$relative] = Get-Sha256 -Path $file.FullName
    }
    return $result
}

function Assert-ReferenceMatchesBaseline {
    if (-not (Test-Path -LiteralPath $baselineRoot -PathType Container)) {
        throw "Reference baseline is missing: '$baselineRoot'. Generate and review it, then run 'dev.cmd reference -UpdateBaseline'."
    }

    $actual = Get-TreeHashes -Root $outputRoot
    $expected = Get-TreeHashes -Root $baselineRoot
    $actualKeys = @($actual.Keys)
    $expectedKeys = @($expected.Keys)
    $differences = New-Object System.Collections.Generic.List[string]

    foreach ($path in $expectedKeys) {
        if (-not $actual.Contains($path)) {
            $differences.Add("missing output: $path")
        }
        elseif ([string] $actual[$path] -ne [string] $expected[$path]) {
            $differences.Add("content differs: $path")
        }
    }
    foreach ($path in $actualKeys) {
        if (-not $expected.Contains($path)) {
            $differences.Add("unexpected output: $path")
        }
    }

    if ($differences.Count -gt 0) {
        throw "Python reference output differs from the reviewed baseline:`n - $($differences -join "`n - ")"
    }

    Write-Host "Reference baseline verified: $($actualKeys.Count) files match."
}

function Update-ReferenceBaseline {
    Ensure-Directory -Path $baselineRoot
    Assert-NoReparsePoints -Path $baselineRoot
    foreach ($item in @(Get-ChildItem -LiteralPath $baselineRoot -Force)) {
        $safeItem = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path $item.FullName `
            -AllowedTopLevelNames @('fixtures')
        if ($WhatIfPreference) {
            Write-Host "What if: remove superseded baseline '$safeItem'."
        }
        else {
            Remove-Item -LiteralPath $safeItem -Recurse -Force
        }
    }

    foreach ($file in @(Get-ChildItem -LiteralPath $outputRoot -File -Recurse)) {
        $relative = $file.FullName.Substring($outputRoot.Length).TrimStart('\', '/')
        $destination = Join-Path $baselineRoot $relative
        if ($PSCmdlet.ShouldProcess($destination, 'Copy reviewed Python reference output into the tracked baseline')) {
            Ensure-Directory -Path (Split-Path -Parent $destination)
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        }
    }
    Write-Host "Reference baseline updated: $baselineRoot"
}

Ensure-Directory -Path $referenceTempRoot
Ensure-Directory -Path $logsRoot
Initialize-UpstreamCheckout
Install-ReferenceDependencies
Reset-OutputDirectory

if ($WhatIfPreference) {
    Write-Host "What if: run the pinned Python profile, IDD, and reference generators into '$outputRoot'."
    exit 0
}

$env:PYTHONHASHSEED = '0'
$env:PYTHONUTF8 = '1'
$upstreamSource = Join-Path $UpstreamPath 'src'
$profileOraclePath = Join-Path $outputRoot 'usage-profile-schedule-oracle.json'
$profileGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $profileGeneratorPath,
    '--',
    '--output', $profileOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $profileGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-usage-profile-reference.log') `
    -FailureMessage 'Generating the Python usage-profile reference oracle failed'

$iddOraclePath = Join-Path $outputRoot 'idd-24.2.0.schema.json.gz'
$iddGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $iddGeneratorPath,
    '--',
    '--idd', $energyPlusIddPath,
    '--epjson-schema', $energyPlusEpJsonSchemaPath,
    '--output', $iddOraclePath,
    '--upstream-commit', $upstreamCommit,
    '--expected-sha256', $requiredEnergyPlusIddSha256,
    '--expected-epjson-sha256', $requiredEnergyPlusEpJsonSchemaSha256,
    '--expected-version', $requiredEnergyPlusVersion,
    '--expected-build', $requiredEnergyPlusBuild
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $iddGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-idd-reference.log') `
    -FailureMessage 'Generating the EnergyPlus IDD reference oracle failed'

$generatorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $generatorPath,
    '--',
    '--repository-root', $repositoryRoot,
    '--upstream-root', $UpstreamPath,
    '--output', $outputRoot
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $generatorArguments `
    -LogPath (Join-Path $logsRoot 'python-reference.log') `
    -FailureMessage 'Generating the Python reference oracle failed'

if ($UpdateBaseline) {
    Update-ReferenceBaseline
}
if ($Mode -eq 'Verify') {
    Assert-ReferenceMatchesBaseline
}

Write-Host "Python reference output: $outputRoot"

#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $NoRestore,
    [switch] $SkipTests,
    [switch] $SkipArtifactStaging,
    [switch] $RequireEnergyPlus
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
$localSettingsPath = Join-Path $repositoryRoot '.tools\state\local.settings.json'
$nugetConfig = Join-Path $repositoryRoot 'NuGet.config'
$toolsRoot = Join-Path $repositoryRoot '.tools'
$tempRoot = Join-Path $repositoryRoot 'temp'
$logsRoot = Join-Path $tempRoot 'logs'
$testResultsRoot = Join-Path $tempRoot (Join-Path 'test-results' $Configuration)
$buildOutputRoot = Join-Path $tempRoot 'build\bin'
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$reportsRoot = Join-Path $artifactsRoot 'reports'
$requiredSdk = [string] ((Get-Content -LiteralPath (Join-Path $repositoryRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version)

function Get-PropertyValue {
    param(
        [AllowNull()]
        [object] $Object,

        [Parameter(Mandatory = $true)]
        [string] $Name,

        [AllowNull()]
        [object] $DefaultValue = $null
    )

    if ($null -eq $Object) {
        return $DefaultValue
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $DefaultValue
    }

    return $property.Value
}

function Reset-GeneratedArtifacts {
    $safeArtifactsRoot = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $artifactsRoot `
        -AllowedTopLevelNames @('artifacts')

    Ensure-Directory -Path $safeArtifactsRoot
    if (-not (Test-Path -LiteralPath $safeArtifactsRoot -PathType Container)) {
        return
    }

    Assert-NoReparsePoints -Path $safeArtifactsRoot -AnchorPath $repositoryRoot
    $generatedItems = @(Get-ChildItem -LiteralPath $safeArtifactsRoot -Force |
        Where-Object { -not $_.Name.Equals('README.md', [System.StringComparison]::OrdinalIgnoreCase) })

    foreach ($item in $generatedItems) {
        $safeItem = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path $item.FullName `
            -AllowedTopLevelNames @('artifacts')
        if ($WhatIfPreference) {
            Write-Host "What if: remove previous generated artifact '$safeItem'."
        }
        else {
            Remove-Item -LiteralPath $safeItem -Recurse -Force
        }
    }
}

function Get-PluginOutputIdentity {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo] $PluginFile
    )

    $module = $null
    if ($PluginFile.Name -match '^GonieGonie\.InvisibleDragon\.GH\.(?:gha|dll)$') {
        $module = 'invisible-dragon'
    }
    elseif ($PluginFile.Name -match '^GonieGonie\.SimpleDragon\.GH\.(?:gha|dll)$') {
        $module = 'simple-dragon'
    }
    else {
        return $null
    }

    $target = $null
    $targetFramework = $null
    $frameworkDirectory = $null
    if ($PluginFile.FullName -match '(?:^|\\)(net48)(?:\\|$)') {
        $target = 'rhino7'
        $targetFramework = $Matches[1]
        $frameworkDirectory = 'net48'
    }
    elseif ($PluginFile.FullName -match '(?:^|\\)(net7\.0-windows[^\\]*)(?:\\|$)') {
        $target = 'rhino8'
        $targetFramework = $Matches[1]
        $frameworkDirectory = 'net7.0'
    }
    elseif ($PluginFile.FullName -match '(?:^|\\)(net8\.0-windows[^\\]*)(?:\\|$)') {
        $target = 'rhino8'
        $targetFramework = $Matches[1]
        $frameworkDirectory = 'net8.0'
    }
    else {
        return $null
    }

    return [pscustomobject] [ordered] @{
        module = $module
        target = $target
        targetFramework = $targetFramework
        frameworkDirectory = $frameworkDirectory
        pluginFile = $PluginFile
    }
}

function Copy-PluginOutput {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Identity
    )

    $sourceDirectory = $Identity.pluginFile.Directory.FullName
    $destination = Join-Path $artifactsRoot (Join-Path $Identity.module (Join-Path $Identity.target $Identity.frameworkDirectory))
    $safeDestination = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $destination `
        -AllowedTopLevelNames @('artifacts')

    if ($WhatIfPreference) {
        Write-Host "What if: stage '$sourceDirectory' to '$safeDestination'."
    }
    else {
        Ensure-Directory -Path $safeDestination
        foreach ($item in @(Get-ChildItem -LiteralPath $sourceDirectory -Force)) {
            # SDK output can include reference-assembly helper directories that
            # are not runtime plugin payloads.
            if ($item.PSIsContainer -and ($item.Name -eq 'ref' -or $item.Name -eq 'refint')) {
                continue
            }
            Copy-Item -LiteralPath $item.FullName -Destination $safeDestination -Recurse -Force
        }
    }

    return [pscustomobject] [ordered] @{
        module = $Identity.module
        target = $Identity.target
        targetFramework = $Identity.targetFramework
        frameworkDirectory = $Identity.frameworkDirectory
        source = $sourceDirectory
        destination = $safeDestination
        entryAssembly = $Identity.pluginFile.Name
    }
}

function Get-GitBuildIdentity {
    $git = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($null -eq $git) {
        $git = Get-Command git -ErrorAction SilentlyContinue
    }

    if ($null -eq $git) {
        return [pscustomobject] [ordered] @{
            commit = $null
            dirty = $null
        }
    }

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $commitOutput = @(& $git.Source -C $repositoryRoot rev-parse HEAD 2>$null)
        $commitExitCode = $LASTEXITCODE
        $statusOutput = @(& $git.Source -C $repositoryRoot status --porcelain 2>$null)
        $statusExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    $commit = $null
    if ($commitExitCode -eq 0 -and $commitOutput.Count -gt 0) {
        $commit = [string] $commitOutput[-1]
    }

    $dirty = $null
    if ($statusExitCode -eq 0) {
        $dirty = $statusOutput.Count -gt 0
    }

    return [pscustomobject] [ordered] @{
        commit = $commit
        dirty = $dirty
    }
}

if (-not (Test-Path -LiteralPath $localSettingsPath -PathType Leaf)) {
    throw "Local setup is missing. Run 'dev.cmd setup' first; expected '$localSettingsPath'."
}

$localSettings = Get-Content -LiteralPath $localSettingsPath -Raw | ConvertFrom-Json
if ([string] (Get-PropertyValue -Object $localSettings -Name 'schema') -ne 'goniegonie.dragons-grasshopper.local-settings.v1') {
    throw "Local settings schema is unsupported. Re-run 'dev.cmd setup'."
}

$dotnetSettings = Get-PropertyValue -Object $localSettings -Name 'dotnet'
$dotnetExecutable = [string] (Get-PropertyValue -Object $dotnetSettings -Name 'executable')
if ([string]::IsNullOrWhiteSpace($dotnetExecutable) -or
    -not (Test-Path -LiteralPath $dotnetExecutable -PathType Leaf)) {
    throw "The setup-selected dotnet executable no longer exists. Re-run 'dev.cmd setup'."
}

$previousErrorActionPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    $actualSdkOutput = @(& $dotnetExecutable --version 2>$null)
    $sdkExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorActionPreference
}

if ($sdkExitCode -ne 0 -or $actualSdkOutput.Count -eq 0 -or [string] $actualSdkOutput[-1] -ne $requiredSdk) {
    $reportedSdk = if ($actualSdkOutput.Count -gt 0) { [string] $actualSdkOutput[-1] } else { '<none>' }
    throw "Configured dotnet does not resolve exact SDK $requiredSdk (reported $reportedSdk). Re-run 'dev.cmd setup'."
}

$solution = Find-SolutionFile -RepositoryRoot $repositoryRoot
if ($null -eq $solution) {
    throw 'No solution file was found. Project scaffolding is not complete, so no build or artifacts were produced.'
}

$rhinoSettings = Get-PropertyValue -Object $localSettings -Name 'rhino'
$rhino7 = Get-PropertyValue -Object $rhinoSettings -Name 'rhino7'
$rhino8 = Get-PropertyValue -Object $rhinoSettings -Name 'rhino8'
$rhino7Ready = [string] (Get-PropertyValue -Object $rhino7 -Name 'status' -DefaultValue 'missing') -eq 'ready'
$rhino8Ready = [string] (Get-PropertyValue -Object $rhino8 -Name 'status' -DefaultValue 'missing') -eq 'ready'

$energyPlusSettings = Get-PropertyValue -Object $localSettings -Name 'energyPlus'
$energyPlusReady = [string] (Get-PropertyValue -Object $energyPlusSettings -Name 'status' -DefaultValue 'missing') -eq 'ready'
if ($RequireEnergyPlus -and -not $energyPlusReady) {
    throw "EnergyPlus was required but no verified runtime is configured. Run 'dev.cmd setup -InstallEnergyPlus'."
}

Set-RepositoryBuildEnvironment -RepositoryRoot $repositoryRoot -DotNetExecutable $dotnetExecutable
Ensure-Directory -Path $logsRoot
Ensure-Directory -Path $testResultsRoot

$env:DRAGONS_ENERGYPLUS_AVAILABLE = if ($energyPlusReady) { '1' } else { '0' }
$env:DRAGONS_ENERGYPLUS_EXE = if ($energyPlusReady) { [string] (Get-PropertyValue -Object $energyPlusSettings -Name 'executable') } else { '' }
$env:GONIEGONIE_RUN_ENERGYPLUS_INTEGRATION = if ($energyPlusReady) { '1' } else { '0' }
$env:GONIEGONIE_ENERGYPLUS_ROOT = if ($energyPlusReady) { [string] (Get-PropertyValue -Object $energyPlusSettings -Name 'root') } else { '' }
$env:DRAGONS_RHINO7_AVAILABLE = if ($rhino7Ready) { '1' } else { '0' }
$env:DRAGONS_RHINO8_AVAILABLE = if ($rhino8Ready) { '1' } else { '0' }
$env:DRAGONS_RHINO7_EXE = if ($rhino7Ready) { [string] (Get-PropertyValue -Object $rhino7 -Name 'executable') } else { '' }
$env:DRAGONS_RHINO8_EXE = if ($rhino8Ready) { [string] (Get-PropertyValue -Object $rhino8 -Name 'executable') } else { '' }

Write-Host "Solution: $solution"
Write-Host ".NET SDK: $requiredSdk"
Write-Host "Rhino 7 runtime tests: $(if ($rhino7Ready) { 'enabled' } else { 'unavailable; version-specific tests will be skipped' })"
Write-Host "Rhino 8 runtime tests: $(if ($rhino8Ready) { 'enabled' } else { 'unavailable; version-specific tests will be skipped' })"
Write-Host "EnergyPlus integration environment: $(if ($energyPlusReady) { 'enabled' } else { 'unavailable' })"

if (-not $NoRestore) {
    if ($PSCmdlet.ShouldProcess($solution, 'Restore NuGet dependencies')) {
        Invoke-WithTrackedPackageLockNormalization `
            -RepositoryRoot $repositoryRoot `
            -Action {
                Invoke-LoggedNativeCommand `
                    -FilePath $dotnetExecutable `
                    -ArgumentList @(
                        'restore', $solution,
                        '--configfile', $nugetConfig,
                        '--packages', (Join-Path $toolsRoot 'nuget\packages'),
                        '--nologo'
                    ) `
                    -LogPath (Join-Path $logsRoot 'restore.log') `
                    -FailureMessage 'Dependency restore failed'
            }
    }
}
else {
    Write-Host 'Restore skipped by -NoRestore.'
}

if ($PSCmdlet.ShouldProcess($solution, "Build $Configuration")) {
    Invoke-LoggedNativeCommand `
        -FilePath $dotnetExecutable `
        -ArgumentList @(
            'build', $solution,
            '--configuration', $Configuration,
            '--no-restore',
            '--nologo'
        ) `
        -LogPath (Join-Path $logsRoot 'build.log') `
        -FailureMessage 'Build failed'
}

$testStatus = 'skipped'
$executedTestProjects = @()
$skippedTestProjects = @()

if ($SkipTests) {
    Write-Host 'Tests skipped by -SkipTests.'
}
else {
    $testsDirectory = Join-Path $repositoryRoot 'tests'
    $testProjects = @()
    if (Test-Path -LiteralPath $testsDirectory -PathType Container) {
        $testProjects = @(Get-ChildItem -LiteralPath $testsDirectory -Filter '*.csproj' -File -Recurse | Sort-Object FullName)
    }

    if ($testProjects.Count -eq 0) {
        $testStatus = 'no-test-projects'
        Write-Warning 'No test projects exist yet. The test stage had nothing to execute.'
    }
    else {
        foreach ($testProject in $testProjects) {
            # Managed xUnit projects carry the RhinoCommon/Grasshopper NuGet
            # runtime assets they need and must also run on clean CI workers.
            # Only the dedicated native-host smoke executables below depend on
            # an installed Rhino generation.
            $projectResults = Join-Path $testResultsRoot $testProject.BaseName
            Ensure-Directory -Path $projectResults
            $safeLogName = ($testProject.BaseName -replace '[^A-Za-z0-9_.-]', '_') + '.log'
            if ($PSCmdlet.ShouldProcess($testProject.FullName, "Test $Configuration")) {
                Invoke-LoggedNativeCommand `
                    -FilePath $dotnetExecutable `
                    -ArgumentList @(
                        'test', $testProject.FullName,
                        '--configuration', $Configuration,
                        '--no-restore',
                        '--no-build',
                        '--results-directory', $projectResults,
                        '--logger', ("trx;LogFileName=$($testProject.BaseName).trx"),
                        '--nologo'
                    ) `
                    -LogPath (Join-Path $logsRoot ('test-' + $safeLogName)) `
                    -FailureMessage "Tests failed for $($testProject.BaseName)"
            }
            $executedTestProjects += $testProject.FullName
        }

        if ($executedTestProjects.Count -gt 0) {
            $testStatus = 'passed'
        }
        elseif ($skippedTestProjects.Count -gt 0) {
            $testStatus = 'runtime-dependent-tests-skipped'
        }
    }

    # The installer path resolver is PowerShell-only, so keep its regression
    # suite in the normal build gate instead of leaving it as a manual check.
    $installerTestScript = Join-Path $repositoryRoot 'tests\Installer\run.ps1'
    if (Test-Path -LiteralPath $installerTestScript -PathType Leaf) {
        $windowsPowerShell = Join-Path $PSHOME 'powershell.exe'
        if ($PSCmdlet.ShouldProcess($installerTestScript, 'Run installer Rhino path checks')) {
            Invoke-LoggedNativeCommand `
                -FilePath $windowsPowerShell `
                -ArgumentList @(
                    '-NoLogo',
                    '-NoProfile',
                    '-ExecutionPolicy', 'Bypass',
                    '-File', $installerTestScript
                ) `
                -LogPath (Join-Path $logsRoot 'test-GonieGonie.Dragons.Installer.log') `
                -FailureMessage 'Installer Rhino path checks failed'
        }
        $executedTestProjects += $installerTestScript
    }

    # Temp retention deletes developer evidence, so its path and lease safety
    # rules stay in the normal gate as a small PowerShell regression suite.
    $tempLifecycleTestScript = Join-Path $repositoryRoot 'tests\TempLifecycle\run.ps1'
    if (Test-Path -LiteralPath $tempLifecycleTestScript -PathType Leaf) {
        $windowsPowerShell = Join-Path $PSHOME 'powershell.exe'
        if ($PSCmdlet.ShouldProcess($tempLifecycleTestScript, 'Run repository temp lifecycle checks')) {
            Invoke-LoggedNativeCommand `
                -FilePath $windowsPowerShell `
                -ArgumentList @(
                    '-NoLogo',
                    '-NoProfile',
                    '-ExecutionPolicy', 'Bypass',
                    '-File', $tempLifecycleTestScript
                ) `
                -LogPath (Join-Path $logsRoot 'test-GonieGonie.Dragons.TempLifecycle.log') `
                -FailureMessage 'Repository temp lifecycle checks failed'
        }
        $executedTestProjects += $tempLifecycleTestScript
    }

    # RhinoCommon's native geometry API must be hosted on an STA thread. These
    # dedicated executables initialize Rhino 8 through Rhino.Inside and cover
    # both Dragon geometry adapters with real Breps.
    $rhinoSmokeChecks = @(
        [pscustomobject] [ordered] @{
            project = Join-Path $repositoryRoot 'tools\rhino-smoke\GonieGonie.InvisibleDragon.Rhino.Smoke.csproj'
            executable = Join-Path $buildOutputRoot (
                Join-Path 'GonieGonie.InvisibleDragon.Rhino.Smoke' (
                    Join-Path $Configuration 'net8.0-windows\GonieGonie.InvisibleDragon.Rhino.Smoke.exe'))
            label = 'GonieGonie.InvisibleDragon.Rhino.Smoke'
        },
        [pscustomobject] [ordered] @{
            project = Join-Path $repositoryRoot 'tools\simpledragon-rhino-smoke\GonieGonie.SimpleDragon.Rhino.Smoke.csproj'
            executable = Join-Path $buildOutputRoot (
                Join-Path 'GonieGonie.SimpleDragon.Rhino.Smoke' (
                    Join-Path $Configuration 'net8.0-windows\GonieGonie.SimpleDragon.Rhino.Smoke.exe'))
            label = 'GonieGonie.SimpleDragon.Rhino.Smoke'
        }
    )
    foreach ($rhinoSmoke in $rhinoSmokeChecks) {
        if (-not (Test-Path -LiteralPath $rhinoSmoke.project -PathType Leaf)) {
            continue
        }

        if ($rhino8Ready) {
            if ($PSCmdlet.ShouldProcess($rhinoSmoke.executable, "Run $($rhinoSmoke.label) Rhino 8 STA geometry smoke checks")) {
                if (-not (Test-Path -LiteralPath $rhinoSmoke.executable -PathType Leaf)) {
                    throw "The Rhino smoke executable was not built at '$($rhinoSmoke.executable)'."
                }

                Invoke-LoggedNativeCommand `
                    -FilePath $rhinoSmoke.executable `
                    -LogPath (Join-Path $logsRoot ("test-$($rhinoSmoke.label).log")) `
                    -FailureMessage "$($rhinoSmoke.label) Rhino 8 geometry smoke checks failed"
            }
            $executedTestProjects += $rhinoSmoke.project
        }
        else {
            $skippedTestProjects += [pscustomobject] [ordered] @{
                project = $rhinoSmoke.project
                reason = 'Rhino 8 is unavailable'
            }
        }
    }

    # Loading a GHA is host behavior, not a managed reflection substitute. Run
    # the repository gate in every installed Rhino generation and verify that a
    # Grasshopper document can be saved and reopened with persistent Dragon Goo.
    $grasshopperSmokeScript = Join-Path $repositoryRoot 'tools\grasshopper-smoke\run.ps1'
    if (Test-Path -LiteralPath $grasshopperSmokeScript -PathType Leaf) {
        if ($rhino7Ready -or $rhino8Ready) {
            $grasshopperTarget = if ($rhino7Ready -and $rhino8Ready) {
                'All'
            }
            elseif ($rhino8Ready) {
                'Rhino8'
            }
            else {
                'Rhino7'
            }
            $windowsPowerShell = Join-Path $PSHOME 'powershell.exe'
            $grasshopperArguments = @(
                '-NoLogo',
                '-NoProfile',
                '-ExecutionPolicy', 'Bypass',
                '-File', $grasshopperSmokeScript,
                '-Target', $grasshopperTarget
            )
            if ($Configuration -eq 'Release') {
                $grasshopperArguments += '-SkipPluginBuild'
            }
            if ($rhino8Ready) {
                $grasshopperArguments += @('-Rhino8Exe', [string] (Get-PropertyValue -Object $rhino8 -Name 'executable'))
            }
            if ($rhino7Ready) {
                $grasshopperArguments += @('-Rhino7Exe', [string] (Get-PropertyValue -Object $rhino7 -Name 'executable'))
            }

            if ($PSCmdlet.ShouldProcess($grasshopperSmokeScript, "Run Grasshopper real-host gate for $grasshopperTarget")) {
                Invoke-LoggedNativeCommand `
                    -FilePath $windowsPowerShell `
                    -ArgumentList $grasshopperArguments `
                    -LogPath (Join-Path $logsRoot 'test-GonieGonie.Dragons.Grasshopper.Host.log') `
                    -FailureMessage 'Grasshopper real-host smoke checks failed'
            }
            $executedTestProjects += $grasshopperSmokeScript
        }
        else {
            $skippedTestProjects += [pscustomobject] [ordered] @{
                project = $grasshopperSmokeScript
                reason = 'no Rhino runtime is available'
            }
        }
    }

    if ($executedTestProjects.Count -gt 0) {
        $testStatus = 'passed'
    }
    elseif ($skippedTestProjects.Count -gt 0) {
        $testStatus = 'runtime-dependent-tests-skipped'
    }
}

$stagedPlugins = @()
if ($SkipArtifactStaging) {
    Write-Host 'Artifact staging skipped by -SkipArtifactStaging.'
}
else {
    Reset-GeneratedArtifacts
    Ensure-Directory -Path $reportsRoot

    $identities = @()
    if (Test-Path -LiteralPath $buildOutputRoot -PathType Container) {
        $pluginFiles = @(Get-ChildItem -LiteralPath $buildOutputRoot -File -Recurse |
            Where-Object {
                $_.Name -match '^GonieGonie\.(?:InvisibleDragon|SimpleDragon)\.GH\.(?:gha|dll)$' -and
                $_.FullName -match ('\\' + [regex]::Escape($Configuration) + '\\') -and
                $_.FullName -notmatch '\\(?:ref|refint)\\'
            })

        foreach ($pluginFile in $pluginFiles) {
            $identity = Get-PluginOutputIdentity -PluginFile $pluginFile
            if ($null -ne $identity) {
                $identities += $identity
            }
        }
    }

    foreach ($group in @($identities | Group-Object module, target, frameworkDirectory)) {
        $preferred = @($group.Group | Where-Object { $_.pluginFile.Extension -eq '.gha' } | Sort-Object { $_.pluginFile.LastWriteTimeUtc } -Descending)
        if ($preferred.Count -eq 0) {
            $preferred = @($group.Group | Sort-Object { $_.pluginFile.LastWriteTimeUtc } -Descending)
        }
        if ($preferred.Count -gt 0) {
            $stagedPlugins += Copy-PluginOutput -Identity $preferred[0]
        }
    }

    if ($stagedPlugins.Count -eq 0) {
        Write-Warning 'No InvisibleDragon.GH or SimpleDragon.GH output was found to stage. Build reports will still be written.'
    }
}

$artifactChecksums = @()
if (-not $SkipArtifactStaging -and -not $WhatIfPreference -and (Test-Path -LiteralPath $artifactsRoot -PathType Container)) {
    foreach ($file in @(Get-ChildItem -LiteralPath $artifactsRoot -File -Recurse |
        Where-Object { $_.FullName -notmatch '\\reports\\' -and $_.Name -ne 'README.md' } |
        Sort-Object FullName)) {
        $relativePath = $file.FullName.Substring($artifactsRoot.Length).TrimStart('\', '/') -replace '\\', '/'
        $artifactChecksums += [pscustomobject] [ordered] @{
            path = $relativePath
            sha256 = Get-Sha256 -Path $file.FullName
        }
    }
}

$gitIdentity = Get-GitBuildIdentity
$buildManifest = [ordered] @{
    schema = 'goniegonie.dragons-grasshopper.build-manifest.v1'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    configuration = $Configuration
    solution = $solution
    dotnetSdk = $requiredSdk
    git = $gitIdentity
    runtimeAvailability = [ordered] @{
        energyPlus = $energyPlusReady
        rhino7 = $rhino7Ready
        rhino8 = $rhino8Ready
    }
    tests = [ordered] @{
        status = $testStatus
        executedProjects = @($executedTestProjects)
        skippedProjects = @($skippedTestProjects)
        resultsDirectory = $testResultsRoot
    }
    stagedPlugins = @($stagedPlugins)
    artifactChecksums = @($artifactChecksums)
}

if (-not $SkipArtifactStaging) {
    Write-Utf8JsonIfChanged `
        -InputObject $buildManifest `
        -Path (Join-Path $reportsRoot 'build-manifest.json') `
        -Depth 12

    $testSummary = [ordered] @{
        schema = 'goniegonie.dragons-grasshopper.test-summary.v1'
        status = $testStatus
        executedProjects = @($executedTestProjects)
        skippedProjects = @($skippedTestProjects)
        resultsDirectory = $testResultsRoot
    }
    Write-Utf8JsonIfChanged `
        -InputObject $testSummary `
        -Path (Join-Path $reportsRoot 'test-summary.json') `
        -Depth 8
}

Write-Host ''
Write-Host "Build complete: $Configuration"
Write-Host "Tests: $testStatus"
if (-not $SkipArtifactStaging) {
    Write-Host "Staged plugin targets: $($stagedPlugins.Count)"
    Write-Host "Artifacts: $artifactsRoot"
}

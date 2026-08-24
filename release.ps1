#requires -Version 5.1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'scripts\common.ps1')

$repositoryRoot = [System.IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\', '/')
$releaseStartedUtc = [DateTime]::UtcNow
$releaseStamp = $releaseStartedUtc.ToString('yyyyMMdd-HHmmss-fff')
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$packagesRoot = Join-Path $artifactsRoot 'packages'
$reportsRoot = Join-Path $artifactsRoot 'reports'
$finalReleaseRoot = Join-Path $artifactsRoot 'release'
$releaseScratchRoot = Join-Path $repositoryRoot 'temp\release-candidate'
$releaseRoot = Join-Path $releaseScratchRoot ("staging-" + $releaseStamp)
$hostReportRoot = Join-Path $releaseRoot 'portable-host-gate'
$settingsPath = Join-Path $repositoryRoot '.config\local.settings.json'

function Resolve-GitExecutable {
    $command = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        $command = Get-Command git -ErrorAction SilentlyContinue
    }

    if ($null -eq $command) {
        throw 'Git is required to create a release candidate.'
    }

    return $command.Source
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $FailureMessage
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $script:gitExecutable -C $repositoryRoot @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        $details = @($output | ForEach-Object { [string] $_ }) -join [Environment]::NewLine
        throw "$FailureMessage (exit code $exitCode).`n$details"
    }

    return @($output | ForEach-Object { [string] $_ })
}

function Invoke-RepositoryCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [string[]] $Arguments = @(),

        [Parameter(Mandatory = $true)]
        [string] $FailureMessage
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required release command is missing: '$Path'."
    }

    & $Path @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit code $LASTEXITCODE)."
    }
}

function Assert-ReleaseSourceClean {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Stage
    )

    $status = @(Invoke-Git `
        -Arguments @('status', '--porcelain', '--untracked-files=all') `
        -FailureMessage "Git status failed during $Stage")
    $unexpected = @($status | Where-Object {
        $_ -notmatch '^\?\? artifacts/' -and
        $_ -notmatch '^\?\? temp/'
    })
    if ($unexpected.Count -ne 0) {
        throw "Release source is not clean during ${Stage}:`n$($unexpected -join [Environment]::NewLine)"
    }
}

function Test-PathWithin {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Candidate
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $candidateFull = [System.IO.Path]::GetFullPath($Candidate)
    return $candidateFull.StartsWith(
        $rootFull + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparseAncestorChain {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Candidate
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $current = [System.IO.Path]::GetFullPath($Candidate).TrimEnd('\', '/')
    if (-not $current.Equals($rootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not (Test-PathWithin -Root $rootFull -Candidate $current)) {
        throw "Path '$current' is outside '$rootFull'."
    }

    while ($true) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Release paths may not traverse reparse point '$current'."
            }
        }

        if ($current.Equals($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }

        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or
            $parent.Equals($current, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Could not reach release path root '$rootFull' from '$current'."
        }
        $current = $parent.TrimEnd('\', '/')
    }
}

function Get-RelativeUnixPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-PathWithin -Root $rootFull -Candidate $pathFull)) {
        throw "Path '$pathFull' is outside '$rootFull'."
    }

    return $pathFull.Substring($rootFull.Length + 1) -replace '\\', '/'
}

function Require-Json {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Schema
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required release report is missing: '$Path'."
    }

    $json = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ([string] $json.schema -ne $Schema) {
        throw "Report '$Path' has schema '$($json.schema)' instead of '$Schema'."
    }

    return $json
}

function Resolve-IndexedPackageArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Artifact,

        [Parameter(Mandatory = $true)]
        [string] $Label,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedExtension
    )

    if ([string]::IsNullOrWhiteSpace($Artifact) -or
        $Artifact.Contains('\') -or
        $Artifact.StartsWith('/') -or
        $Artifact -match '^[A-Za-z]:' -or
        @($Artifact.Split('/') | Where-Object {
            $_ -eq '.' -or $_ -eq '..' -or [string]::IsNullOrWhiteSpace($_)
        }).Count -ne 0) {
        throw "Package index contains an unsafe or non-canonical artifact path for ${Label}: '$Artifact'."
    }

    $path = [System.IO.Path]::GetFullPath((
        Join-Path $packagesRoot ($Artifact -replace '/', '\')
    ))
    if (-not (Test-PathWithin -Root $packagesRoot -Candidate $path) -or
        -not (Test-Path -LiteralPath $path -PathType Leaf) -or
        -not [System.IO.Path]::GetExtension($path).Equals(
            $ExpectedExtension,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Package index artifact for '$Label' is absent, outside the package root, or has the wrong type."
    }
    Assert-NoReparseAncestorChain -Root $repositoryRoot -Candidate $path
    return $path
}

function Initialize-ReleaseWorkspace {
    $safeScratchRoot = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $releaseScratchRoot `
        -AllowedTopLevelNames @('temp')
    Assert-NoReparseAncestorChain -Root $repositoryRoot -Candidate $safeScratchRoot
    Ensure-Directory -Path $safeScratchRoot
    Assert-NoReparseAncestorChain -Root $repositoryRoot -Candidate $safeScratchRoot
    Assert-NoReparsePoints -Path $safeScratchRoot

    if (Test-Path -LiteralPath $releaseRoot) {
        throw "Release staging directory already exists: '$releaseRoot'."
    }

    if (Test-Path -LiteralPath $finalReleaseRoot) {
        $safeExisting = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path $finalReleaseRoot `
            -AllowedTopLevelNames @('artifacts')
        Assert-NoReparseAncestorChain -Root $repositoryRoot -Candidate $safeExisting
        Assert-NoReparsePoints -Path $safeExisting
        $archivePath = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path (Join-Path $safeScratchRoot ("previous-" + $releaseStamp)) `
            -AllowedTopLevelNames @('temp')
        Assert-NoReparseAncestorChain -Root $repositoryRoot -Candidate $archivePath
        if (Test-Path -LiteralPath $archivePath) {
            throw "Previous-release archive path already exists: '$archivePath'."
        }

        Move-Item -LiteralPath $safeExisting -Destination $archivePath
        Write-Host "Moved the previous generated release report to '$archivePath'."
    }

    Ensure-Directory -Path $releaseRoot
    Assert-NoReparsePoints -Path $releaseRoot
}

function Publish-ReleaseWorkspace {
    $safeStaging = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $releaseRoot `
        -AllowedTopLevelNames @('temp')
    $safeFinal = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $finalReleaseRoot `
        -AllowedTopLevelNames @('artifacts')
    Assert-NoReparseAncestorChain -Root $repositoryRoot -Candidate $safeStaging
    Assert-NoReparseAncestorChain -Root $repositoryRoot -Candidate $safeFinal
    Assert-NoReparsePoints -Path $safeStaging
    if (Test-Path -LiteralPath $safeFinal) {
        throw "Refusing to replace an unexpected release directory: '$safeFinal'."
    }

    Ensure-Directory -Path $artifactsRoot
    Assert-NoReparseAncestorChain -Root $repositoryRoot -Candidate $artifactsRoot
    Move-Item -LiteralPath $safeStaging -Destination $safeFinal
}

function Get-PortableHostGateRunPaths {
    $smokeRoot = Join-Path $repositoryRoot 'temp\grasshopper-smoke'
    if (-not (Test-Path -LiteralPath $smokeRoot -PathType Container)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $smokeRoot -Directory |
        ForEach-Object { $_.FullName })
}

function Find-PortableHostGateRun {
    param(
        [string[]] $ExistingPaths = @()
    )

    $smokeRoot = Join-Path $repositoryRoot 'temp\grasshopper-smoke'
    if (-not (Test-Path -LiteralPath $smokeRoot -PathType Container)) {
        throw 'The portable package host gate produced no run directory.'
    }

    $candidates = @(Get-ChildItem -LiteralPath $smokeRoot -Directory |
        Where-Object {
            $ExistingPaths -notcontains $_.FullName -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'PASS.txt') -PathType Leaf) -and
            @(Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Filter 'summary.json').Count -eq 6
        } |
        Sort-Object LastWriteTimeUtc -Descending)
    if ($candidates.Count -ne 1) {
        throw "Expected exactly one new six-scenario host-gate run from the package command; found $($candidates.Count)."
    }

    return $candidates[0]
}

$script:gitExecutable = Resolve-GitExecutable
Assert-ReleaseSourceClean -Stage 'preflight'

$branchOutput = @(Invoke-Git `
    -Arguments @('branch', '--show-current') `
    -FailureMessage 'Could not read the current branch')
$branch = [string] $branchOutput[-1]
if ($branch -ne 'main') {
    throw "Release candidates must be built from main; current branch is '$branch'."
}

$commitOutput = @(Invoke-Git `
    -Arguments @('rev-parse', 'HEAD') `
    -FailureMessage 'Could not read HEAD')
$commit = [string] $commitOutput[-1]
$originOutput = @(Invoke-Git `
    -Arguments @('remote', 'get-url', 'origin') `
    -FailureMessage 'Could not read origin URL')
$originUrl = [string] $originOutput[-1]
if ($originUrl -notmatch '(?i)(?:github\.com[/:])Gonie-Gonie/EPlusSimple-Grasshopper(?:\.git)?$') {
    throw "Origin is not the Gonie-Gonie EPlusSimple-Grasshopper repository: '$originUrl'."
}

$remoteRows = @(Invoke-Git `
    -Arguments @('ls-remote', '--exit-code', 'origin', 'refs/heads/main') `
    -FailureMessage 'Could not verify origin/main')
$remoteMatch = @($remoteRows | Where-Object { $_ -match '^(?<commit>[0-9a-fA-F]{40})\s+refs/heads/main$' })
if ($remoteMatch.Count -ne 1) {
    throw 'origin/main did not resolve to exactly one commit.'
}
$null = $remoteMatch[0] -match '^(?<commit>[0-9a-fA-F]{40})\s+'
$remoteCommit = [string] $Matches['commit']
if (-not $remoteCommit.Equals($commit, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "HEAD '$commit' has not been pushed to origin/main '$remoteCommit'."
}

Initialize-ReleaseWorkspace

Write-Host "Release candidate source: $commit"
Write-Host 'Bootstrapping the pinned SDK, Python, Rhino checks, and EnergyPlus runtime...'
Invoke-RepositoryCommand `
    -Path (Join-Path $repositoryRoot 'setup.cmd') `
    -Arguments @('-InstallEnergyPlus', '-RequireEnergyPlus', '-RequireRhino7', '-RequireRhino8') `
    -FailureMessage 'Release environment setup failed'

Write-Host 'Verifying the pinned Python compatibility oracle...'
Invoke-RepositoryCommand `
    -Path (Join-Path $repositoryRoot 'reference.cmd') `
    -Arguments @('-Mode', 'Verify') `
    -FailureMessage 'Python compatibility oracle failed'

Write-Host 'Building and testing all Rhino targets with EnergyPlus integration...'
Invoke-RepositoryCommand `
    -Path (Join-Path $repositoryRoot 'build.cmd') `
    -Arguments @('-NoRestore', '-RequireEnergyPlus') `
    -FailureMessage 'Release build failed'

Write-Host 'Opening and round-trip validating the tracked examples in Rhino 7 and Rhino 8...'
Invoke-RepositoryCommand `
    -Path (Join-Path $repositoryRoot 'tools\example-definitions\run.cmd') `
    -Arguments @('-SkipPluginBuild') `
    -FailureMessage 'Verified Grasshopper example gate failed'

Write-Host 'Packaging and loading the exact portable ZIPs in six fresh Rhino hosts...'
$hostRunsBeforePackage = @(Get-PortableHostGateRunPaths)
Invoke-RepositoryCommand `
    -Path (Join-Path $repositoryRoot 'package.cmd') `
    -Arguments @('-SkipBuild', '-RunPortableHostGate') `
    -FailureMessage 'Release packaging or portable host verification failed'

Assert-ReleaseSourceClean -Stage 'post-verification'

$settings = Require-Json `
    -Path $settingsPath `
    -Schema 'goniegonie.dragons-grasshopper.local-settings.v1'
$buildManifestPath = Join-Path $reportsRoot 'build-manifest.json'
$testSummaryPath = Join-Path $reportsRoot 'test-summary.json'
$packageIndexPath = Join-Path $packagesRoot 'package-index.json'
$compatibilityPath = Join-Path $packagesRoot 'compatibility-report.json'
$packageChecksumsPath = Join-Path $packagesRoot 'checksums.sha256'
$buildManifest = Require-Json `
    -Path $buildManifestPath `
    -Schema 'goniegonie.dragons-grasshopper.build-manifest.v1'
$testSummary = Require-Json `
    -Path $testSummaryPath `
    -Schema 'goniegonie.dragons-grasshopper.test-summary.v1'
$packageIndex = Require-Json `
    -Path $packageIndexPath `
    -Schema 'goniegonie.dragons-grasshopper.package-index.v1'
$compatibilityReport = Require-Json `
    -Path $compatibilityPath `
    -Schema 'goniegonie.dragons-grasshopper.package-verification.v1'
if (-not [bool] $compatibilityReport.success -or
    @($compatibilityReport.failures).Count -ne 0) {
    throw 'Package compatibility report records a failure.'
}
foreach ($scenarioName in @('InvisibleDragon-only', 'SimpleDragon-only', 'both')) {
    $scenarioProperty = $compatibilityReport.scenarios.PSObject.Properties[$scenarioName]
    if ($null -eq $scenarioProperty -or -not [bool] $scenarioProperty.Value) {
        throw "Package compatibility scenario '$scenarioName' did not pass."
    }
}
if (-not (Test-Path -LiteralPath $packageChecksumsPath -PathType Leaf)) {
    throw 'Package checksum file is missing.'
}
if ([string] $testSummary.status -ne 'passed') {
    throw "Release tests did not report passed status: '$($testSummary.status)'."
}
if ([string] $buildManifest.git.commit -ne $commit -or [bool] $buildManifest.git.dirty) {
    throw 'Build manifest does not identify the clean release commit.'
}
if (-not [bool] $buildManifest.runtimeAvailability.energyPlus -or
    -not [bool] $buildManifest.runtimeAvailability.rhino7 -or
    -not [bool] $buildManifest.runtimeAvailability.rhino8) {
    throw 'Build manifest does not attest EnergyPlus, Rhino 7, and Rhino 8 availability.'
}

$expectedProductNames = @{
    'invisible-dragon' = 'InvisibleDragon'
    'simple-dragon' = 'SimpleDragon'
}
$productRows = @($packageIndex.products)
$actualProductIds = @($productRows | ForEach-Object { [string] $_.id } | Sort-Object)
if ($productRows.Count -ne 2 -or
    @(Compare-Object `
            -ReferenceObject @($expectedProductNames.Keys | Sort-Object) `
            -DifferenceObject $actualProductIds).Count -ne 0) {
    throw 'Package index must identify exactly one invisible-dragon and one simple-dragon product.'
}

$portableExpectations = @{}
$indexedBinaryExpectations = @()
foreach ($product in $productRows) {
    $productId = [string] $product.id
    $displayName = [string] $product.name
    if ($displayName -ne [string] $expectedProductNames[$productId] -or
        [string] $product.version -ne [string] $packageIndex.version) {
        throw "Package index identity/version mismatch for '$productId'."
    }

    $archiveArtifact = [string] $product.portable.artifact
    $archivePath = Resolve-IndexedPackageArtifact `
        -Artifact $archiveArtifact `
        -Label "$displayName portable archive" `
        -ExpectedExtension '.zip'
    $expectedHash = [string] $product.portable.sha256
    if ($expectedHash -notmatch '^[0-9a-fA-F]{64}$' -or
        -not (Get-Sha256 -Path $archivePath).Equals(
            $expectedHash,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Package index portable SHA-256 is invalid for '$displayName'."
    }
    $portableExpectations[$displayName] = [pscustomobject] [ordered] @{
        path = $archivePath
        sha256 = $expectedHash.ToLowerInvariant()
    }
    $indexedBinaryExpectations += [pscustomobject] [ordered] @{
        product = $displayName
        kind = 'portable'
        target = 'all'
        path = $archivePath
        sha256 = $expectedHash.ToLowerInvariant()
    }

    $yakRows = @($product.yak)
    $yakTargets = @($yakRows | ForEach-Object { [string] $_.target } | Sort-Object)
    if ($yakRows.Count -ne 2 -or
        @(Compare-Object `
                -ReferenceObject @('rhino7', 'rhino8') `
                -DifferenceObject $yakTargets).Count -ne 0) {
        throw "Package index must identify exactly the Rhino 7 and Rhino 8 Yak artifacts for '$displayName'."
    }

    foreach ($yak in $yakRows) {
        $target = [string] $yak.target
        $major = if ($target -eq 'rhino7') { '7' } else { '8' }
        $emittedFilename = [string] $yak.emittedFilename
        $distributionTag = [string] $yak.distributionTag
        $emittedPattern = '^(?<prefix>' +
            [regex]::Escape($productId + '-' + [string] $packageIndex.version + '-') +
            'rh' + $major + '(?:_\d+)?-win)\.yak$'
        $emittedMatch = [regex]::Match(
            $emittedFilename,
            $emittedPattern,
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        $expectedArtifact = '{0}/yak/{0}-{1}-rh{2}-win.yak' -f `
            $productId,
            [string] $packageIndex.version,
            $major
        if (-not $emittedMatch.Success -or
            $distributionTag -ne $emittedMatch.Groups['prefix'].Value.Substring(
                ($productId + '-' + [string] $packageIndex.version + '-').Length) -or
            [string] $yak.artifact -ne $expectedArtifact) {
            throw "Package index Yak identity/path mismatch for '$displayName' $target."
        }

        $yakPath = Resolve-IndexedPackageArtifact `
            -Artifact ([string] $yak.artifact) `
            -Label "$displayName $target Yak archive" `
            -ExpectedExtension '.yak'
        $yakHash = [string] $yak.sha256
        if ($yakHash -notmatch '^[0-9a-fA-F]{64}$' -or
            -not (Get-Sha256 -Path $yakPath).Equals(
                $yakHash,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Package index Yak SHA-256 is invalid for '$displayName' $target."
        }
        $indexedBinaryExpectations += [pscustomobject] [ordered] @{
            product = $displayName
            kind = 'yak'
            target = $target
            path = $yakPath
            sha256 = $yakHash.ToLowerInvariant()
        }
    }
}
if ($portableExpectations.Count -ne 2 -or
    $indexedBinaryExpectations.Count -ne 6 -or
    -not $portableExpectations.ContainsKey('InvisibleDragon') -or
    -not $portableExpectations.ContainsKey('SimpleDragon')) {
    throw 'Package index did not produce the exact two portable and four Yak artifact expectations.'
}

$hostRun = Find-PortableHostGateRun -ExistingPaths $hostRunsBeforePackage
$summaryFiles = @(Get-ChildItem -LiteralPath $hostRun.FullName -Recurse -File -Filter 'summary.json')
$expectedScenarios = @(
    'Rhino7/InvisibleOnly',
    'Rhino7/SimpleOnly',
    'Rhino7/Both',
    'Rhino8/InvisibleOnly',
    'Rhino8/SimpleOnly',
    'Rhino8/Both'
)
$scenarioReports = @()
Ensure-Directory -Path $hostReportRoot
foreach ($summaryFile in $summaryFiles) {
    $summary = Require-Json `
        -Path $summaryFile.FullName `
        -Schema 'goniegonie.dragons-grasshopper.host-smoke.v3'
    $key = [string] $summary.host + '/' + [string] $summary.scenario
    if ($expectedScenarios -notcontains $key) {
        throw "Portable host gate reported an unexpected scenario '$key'."
    }
    if ([string] $summary.source -ne 'portable-package') {
        throw "Portable host scenario '$key' used source '$($summary.source)'."
    }

    $expectedPluginCount = if ([string] $summary.scenario -eq 'Both') { 2 } else { 1 }
    if ([int] $summary.pluginCount -ne $expectedPluginCount) {
        throw "Portable host scenario '$key' reported the wrong plugin count."
    }

    $expectedProducts = switch ([string] $summary.scenario) {
        'InvisibleOnly' { @('InvisibleDragon') }
        'SimpleOnly' { @('SimpleDragon') }
        'Both' { @('InvisibleDragon', 'SimpleDragon') }
        default { throw "Unknown portable host scenario '$($summary.scenario)'." }
    }
    $archiveProvenance = @($summary.portableArchives)
    $pluginProvenance = @($summary.pluginArtifacts)
    if ($archiveProvenance.Count -ne $expectedPluginCount -or
        $pluginProvenance.Count -ne $expectedPluginCount) {
        throw "Portable host scenario '$key' did not attest every archive and loaded GHA."
    }
    if (@(Compare-Object `
            -ReferenceObject @($expectedProducts | Sort-Object) `
            -DifferenceObject @($archiveProvenance | ForEach-Object { [string] $_.product } | Sort-Object)).Count -ne 0 -or
        @(Compare-Object `
            -ReferenceObject @($expectedProducts | Sort-Object) `
            -DifferenceObject @($pluginProvenance | ForEach-Object { [string] $_.product } | Sort-Object)).Count -ne 0) {
        throw "Portable host scenario '$key' attested the wrong product set."
    }

    foreach ($archive in $archiveProvenance) {
        $productName = [string] $archive.product
        $expectedArchive = $portableExpectations[$productName]
        $archivePath = [System.IO.Path]::GetFullPath([string] $archive.path)
        $archiveHash = [string] $archive.sha256
        if (-not $archivePath.Equals(
                [string] $expectedArchive.path,
                [System.StringComparison]::OrdinalIgnoreCase) -or
            -not $archiveHash.Equals(
                [string] $expectedArchive.sha256,
                [System.StringComparison]::OrdinalIgnoreCase) -or
            -not (Get-Sha256 -Path $archivePath).Equals(
                $archiveHash,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Portable host scenario '$key' archive provenance changed for '$productName'."
        }
    }

    $legacyPluginPaths = @($summary.pluginPaths | ForEach-Object {
        [System.IO.Path]::GetFullPath([string] $_)
    } | Sort-Object)
    $attestedPluginPaths = @($pluginProvenance | ForEach-Object {
        [System.IO.Path]::GetFullPath([string] $_.path)
    } | Sort-Object)
    if (@(Compare-Object `
            -ReferenceObject $legacyPluginPaths `
            -DifferenceObject $attestedPluginPaths).Count -ne 0) {
        throw "Portable host scenario '$key' plugin path/provenance sets disagree."
    }
    foreach ($plugin in $pluginProvenance) {
        $pluginPath = [System.IO.Path]::GetFullPath([string] $plugin.path)
        $pluginHash = [string] $plugin.sha256
        if (-not (Test-PathWithin `
                -Root (Join-Path $hostRun.FullName 'portable-extract') `
                -Candidate $pluginPath) -or
            $pluginHash -notmatch '^[0-9a-fA-F]{64}$' -or
            -not (Get-Sha256 -Path $pluginPath).Equals(
                $pluginHash,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Portable host scenario '$key' loaded outside its extracted package: '$pluginPath'."
        }
    }

    $reportName = ([string] $summary.host).ToLowerInvariant() + '-' +
        ([string] $summary.scenario).ToLowerInvariant() + '.json'
    $reportPath = Join-Path $hostReportRoot $reportName
    Copy-Item -LiteralPath $summaryFile.FullName -Destination $reportPath -Force
    $scenarioReports += [pscustomobject] [ordered] @{
        host = [string] $summary.host
        rhinoVersion = [string] $summary.rhinoVersion
        grasshopperVersion = [string] $summary.grasshopperVersion
        scenario = [string] $summary.scenario
        pluginCount = [int] $summary.pluginCount
        invisibleComponents = [int] $summary.registeredInvisibleComponents
        invisibleParameters = [int] $summary.registeredInvisibleParameters
        simpleComponents = [int] $summary.registeredSimpleComponents
        simpleParameters = [int] $summary.registeredSimpleParameters
        reopenedObjects = [int] $summary.reopenedObjectCount
        portableArchives = @($archiveProvenance | Sort-Object product | ForEach-Object {
            [pscustomobject] [ordered] @{
                product = [string] $_.product
                path = Get-RelativeUnixPath -Root $artifactsRoot -Path ([string] $_.path)
                sha256 = ([string] $_.sha256).ToLowerInvariant()
            }
        })
        loadedPlugins = @($pluginProvenance | Sort-Object product | ForEach-Object {
            [pscustomobject] [ordered] @{
                product = [string] $_.product
                fileName = [System.IO.Path]::GetFileName([string] $_.path)
                sha256 = ([string] $_.sha256).ToLowerInvariant()
            }
        })
        report = 'release/' + (Get-RelativeUnixPath -Root $releaseRoot -Path $reportPath)
        sha256 = Get-Sha256 -Path $reportPath
    }
}

$actualScenarios = @($scenarioReports | ForEach-Object { $_.host + '/' + $_.scenario } | Sort-Object)
if (@(Compare-Object `
        -ReferenceObject @($expectedScenarios | Sort-Object) `
        -DifferenceObject $actualScenarios).Count -ne 0) {
    throw 'The portable host gate did not produce all six required host/scenario combinations.'
}

$binaryAssets = @(Get-ChildItem -LiteralPath $packagesRoot -Recurse -File |
    Where-Object { $_.Extension -in @('.yak', '.zip') })
$expectedBinaryPaths = @($indexedBinaryExpectations |
    ForEach-Object { [System.IO.Path]::GetFullPath([string] $_.path) } |
    Sort-Object)
$actualBinaryPaths = @($binaryAssets |
    ForEach-Object { [System.IO.Path]::GetFullPath($_.FullName) } |
    Sort-Object)
if ($binaryAssets.Count -ne 6 -or
    @(Compare-Object `
            -ReferenceObject $expectedBinaryPaths `
            -DifferenceObject $actualBinaryPaths).Count -ne 0) {
    throw 'The generated Yak/portable binary set does not exactly match package-index.json.'
}
foreach ($expectation in $indexedBinaryExpectations) {
    if (-not (Get-Sha256 -Path ([string] $expectation.path)).Equals(
            [string] $expectation.sha256,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Indexed $($expectation.kind) artifact changed for '$($expectation.product)' $($expectation.target)."
    }
}

$releaseAssets = @(
    $binaryAssets
    Get-Item -LiteralPath $packageIndexPath
    Get-Item -LiteralPath $compatibilityPath
    Get-Item -LiteralPath $packageChecksumsPath
)
if (@($releaseAssets | Where-Object { $_.Extension -eq '.yak' }).Count -ne 4 -or
    @($releaseAssets | Where-Object { $_.Extension -eq '.zip' }).Count -ne 2 -or
    $releaseAssets.Count -ne 9) {
    throw "Expected four Yak archives, two portable ZIPs, and three common reports; found $($releaseAssets.Count) release assets."
}
$assetReports = @($releaseAssets | Sort-Object FullName | ForEach-Object {
    [pscustomobject] [ordered] @{
        path = Get-RelativeUnixPath -Root $artifactsRoot -Path $_.FullName
        bytes = [int64] $_.Length
        sha256 = Get-Sha256 -Path $_.FullName
    }
})

$releaseGate = [pscustomobject] [ordered] @{
    schema = 'goniegonie.dragons-grasshopper.release-gate.v1'
    status = 'passed'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    source = [pscustomobject] [ordered] @{
        owner = 'Gonie-Gonie'
        repository = 'Gonie-Gonie/EPlusSimple-Grasshopper'
        branch = 'main'
        commit = $commit
        origin = $originUrl
        pushedToOriginMain = $true
        clean = $true
    }
    candidate = [pscustomobject] [ordered] @{
        version = [string] $packageIndex.version
        products = @($packageIndex.products | ForEach-Object { [string] $_.id })
        rhinoSupport = @('Rhino 7/net48', 'Rhino 8/net7.0', 'Rhino 8/net8.0')
    }
    environment = [pscustomobject] [ordered] @{
        dotnetSdk = [string] $settings.dotnet.sdkVersion
        python = [string] $settings.pythonOracle.version
        energyPlusVersion = [string] $settings.energyPlus.version
        energyPlusBuild = [string] $settings.energyPlus.build
        rhino7 = [string] $settings.rhino.rhino7.version
        rhino8 = [string] $settings.rhino.rhino8.version
    }
    verification = [pscustomobject] [ordered] @{
        pythonOracle = 'passed'
        managedAndIntegrationTests = [string] $testSummary.status
        grasshopperExamples = 'passed'
        packageCompatibility = 'passed'
        buildManifest = [pscustomobject] [ordered] @{
            path = Get-RelativeUnixPath -Root $artifactsRoot -Path $buildManifestPath
            sha256 = Get-Sha256 -Path $buildManifestPath
        }
        testSummary = [pscustomobject] [ordered] @{
            path = Get-RelativeUnixPath -Root $artifactsRoot -Path $testSummaryPath
            sha256 = Get-Sha256 -Path $testSummaryPath
        }
        portableHostGate = @($scenarioReports | Sort-Object host, scenario)
    }
    assets = $assetReports
    publication = [pscustomobject] [ordered] @{
        publicPublicationAuthorized = $false
        tagCreated = $false
        githubReleaseCreated = $false
        yakPublished = $false
        reason = 'This command creates a local verified candidate only. NOTICE.md records an unresolved upstream standalone-license omission that requires review before public binary publication.'
    }
}

$releaseGatePath = Join-Path $releaseRoot 'release-gate.json'
Write-Utf8JsonIfChanged -InputObject $releaseGate -Path $releaseGatePath -Depth 16
$checksumFiles = @(
    Get-Item -LiteralPath $releaseGatePath
    Get-ChildItem -LiteralPath $hostReportRoot -File -Filter '*.json'
)
$checksumLines = @($checksumFiles | Sort-Object FullName | ForEach-Object {
    (Get-Sha256 -Path $_.FullName) + '  ' +
        (Get-RelativeUnixPath -Root $releaseRoot -Path $_.FullName)
})
[System.IO.File]::WriteAllText(
    (Join-Path $releaseRoot 'checksums.sha256'),
    ($checksumLines -join [Environment]::NewLine) + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))

Publish-ReleaseWorkspace

Write-Host ''
Write-Host "Verified local release candidate complete: $finalReleaseRoot"
Write-Host "Version: $($packageIndex.version)"
Write-Host "Commit: $commit"
Write-Host 'No tag, GitHub release, package install, or Yak publication was performed.'

#requires -Version 5.1

[CmdletBinding()]
param(
    [ValidateSet('All', 'Rhino7', 'Rhino8')]
    [string] $Target = 'All',

    [string] $Rhino7Path,

    [string] $Rhino8Path,

    [switch] $UseExistingPackages
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'install-rhino-host.ps1')

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
$packagesRoot = Join-Path $repositoryRoot 'artifacts\packages'
$packageIndexPath = Join-Path $packagesRoot 'package-index.json'
$localSettingsPath = Join-Path $repositoryRoot '.config\local.settings.json'
$runStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$runRoot = Join-Path $repositoryRoot (Join-Path 'temp\install' ("run-" + $runStamp))
$productIds = @('invisible-dragon', 'simple-dragon')

function Invoke-RepositoryCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [string[]] $Arguments = @(),

        [Parameter(Mandatory = $true)]
        [string] $FailureMessage
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required command is missing: '$Path'."
    }

    & $Path @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit code $LASTEXITCODE)."
    }
}

function Invoke-Yak {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Executable,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $LogName,

        [Parameter(Mandatory = $true)]
        [string] $FailureMessage
    )

    $logPath = Join-Path $runRoot $LogName
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $Executable @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    $lines = @($output | ForEach-Object { [string] $_ })
    [System.IO.File]::WriteAllText(
        $logPath,
        ($lines -join [Environment]::NewLine) + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
    foreach ($line in $lines) {
        Write-Host $line
    }
    if ($exitCode -ne 0) {
        throw "$FailureMessage (exit code $exitCode). See '$logPath'."
    }

    return @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Test-PackageListed {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Lines,

        [Parameter(Mandatory = $true)]
        [string] $ProductId,

        [string] $Version
    )

    $versionPattern = if ([string]::IsNullOrWhiteSpace($Version)) {
        '[^)]+'
    }
    else {
        [regex]::Escape($Version)
    }
    $pattern = '^\s*' + [regex]::Escape($ProductId) +
        '\s+\(' + $versionPattern + '\)\s*$'
    return @($Lines | Where-Object { [regex]::IsMatch($_, $pattern) }).Count -eq 1
}

function Resolve-IndexedYak {
    param(
        [Parameter(Mandatory = $true)]
        [object] $PackageIndex,

        [Parameter(Mandatory = $true)]
        [string] $ProductId,

        [Parameter(Mandatory = $true)]
        [string] $PackageTarget
    )

    $products = @($PackageIndex.products | Where-Object { [string] $_.id -eq $ProductId })
    if ($products.Count -ne 1) {
        throw "Package index must contain exactly one '$ProductId' product."
    }

    $yakRows = @($products[0].yak | Where-Object { [string] $_.target -eq $PackageTarget })
    if ($yakRows.Count -ne 1) {
        throw "Package index must contain exactly one '$ProductId'/$PackageTarget Yak artifact."
    }

    $artifact = [string] $yakRows[0].artifact
    if ([string]::IsNullOrWhiteSpace($artifact) -or
        $artifact.Contains('\') -or
        $artifact.StartsWith('/') -or
        $artifact -match '^[A-Za-z]:' -or
        @($artifact.Split('/') | Where-Object {
            $_ -eq '.' -or $_ -eq '..' -or [string]::IsNullOrWhiteSpace($_)
        }).Count -ne 0) {
        throw "Package index contains an unsafe Yak artifact path for '$ProductId'/$PackageTarget."
    }

    $path = [System.IO.Path]::GetFullPath((
        Join-Path $packagesRoot ($artifact -replace '/', '\')
    ))
    $packagePrefix = $packagesRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $path.StartsWith($packagePrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $path -PathType Leaf) -or
        -not [System.IO.Path]::GetExtension($path).Equals(
            '.yak',
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Indexed Yak artifact is absent or outside the package root: '$path'."
    }

    $expectedHash = [string] $yakRows[0].sha256
    if ($expectedHash -notmatch '^[0-9a-fA-F]{64}$' -or
        -not (Get-Sha256 -Path $path).Equals(
            $expectedHash,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Indexed Yak SHA-256 mismatch for '$ProductId'/$PackageTarget."
    }

    return [pscustomobject] [ordered] @{
        product = $ProductId
        target = $PackageTarget
        path = $path
        sha256 = $expectedHash.ToLowerInvariant()
    }
}

$runningRhino = @(Get-Process -Name 'Rhino' -ErrorAction SilentlyContinue)
if ($runningRhino.Count -ne 0) {
    $processIds = @($runningRhino | ForEach-Object { [string] $_.Id }) -join ', '
    throw "Close every Rhino process before reinstalling Dragon packages. Running Rhino process IDs: $processIds."
}

$knownHostSpecifications = @(
    [pscustomobject] [ordered] @{
        name = 'Rhino7'
        majorVersion = 7
        packageTarget = 'rhino7'
        explicitPath = $Rhino7Path
        standardExecutable = 'C:\Program Files\Rhino 7\System\Rhino.exe'
    },
    [pscustomobject] [ordered] @{
        name = 'Rhino8'
        majorVersion = 8
        packageTarget = 'rhino8'
        explicitPath = $Rhino8Path
        standardExecutable = 'C:\Program Files\Rhino 8\System\Rhino.exe'
    }
)
$requestedHostSpecifications = @($knownHostSpecifications | Where-Object {
    $Target -eq 'All' -or $_.name -eq $Target
})

$localSettings = $null
$localSettingsError = $null
try {
    $localSettings = Read-RhinoLocalSettings -Path $localSettingsPath
}
catch {
    $localSettingsError = $_.Exception.Message
}

$hosts = @()
$missingHosts = @()
foreach ($hostSpecification in $requestedHostSpecifications) {
    try {
        if ([string]::IsNullOrWhiteSpace([string] $hostSpecification.explicitPath) -and
            -not [string]::IsNullOrWhiteSpace($localSettingsError)) {
            throw $localSettingsError
        }

        $candidate = Resolve-RhinoHostCandidate `
            -Name ([string] $hostSpecification.name) `
            -MajorVersion ([int] $hostSpecification.majorVersion) `
            -PackageTarget ([string] $hostSpecification.packageTarget) `
            -ExplicitPath ([string] $hostSpecification.explicitPath) `
            -LocalSettings $localSettings `
            -StandardExecutable ([string] $hostSpecification.standardExecutable)
        $confirmedHost = Confirm-RhinoHostCandidate -Candidate $candidate
        $hosts += $confirmedHost
        Write-Host (
            "$($confirmedHost.name): $($confirmedHost.version) at $($confirmedHost.rhino) [$($confirmedHost.source)]")
    }
    catch {
        $missingHosts += [pscustomobject] [ordered] @{
            name = [string] $hostSpecification.name
            reason = $_.Exception.Message
        }
    }
}

if ($Target -ne 'All' -and $missingHosts.Count -ne 0) {
    throw "$Target is unavailable: $($missingHosts[0].reason)"
}
if ($hosts.Count -eq 0) {
    $reasons = @($missingHosts | ForEach-Object { "$($_.name): $($_.reason)" }) -join '; '
    throw "No supported Rhino 7 or Rhino 8 installation was found. $reasons"
}
foreach ($missingHost in $missingHosts) {
    Write-Warning "$($missingHost.name) is unavailable and will be skipped: $($missingHost.reason)"
}

Ensure-Directory -Path $runRoot

if (-not $UseExistingPackages) {
    $setupArguments = @(Get-RhinoSetupArguments -Hosts $hosts)

    Write-Host 'Preparing the reproducible build environment...'
    Invoke-RepositoryCommand `
        -Path (Join-Path $repositoryRoot 'dev.cmd') `
        -Arguments (@('setup') + $setupArguments) `
        -FailureMessage 'Setup failed'

    Write-Host 'Building current Dragon sources without running tests...'
    Invoke-RepositoryCommand `
        -Path (Join-Path $repositoryRoot 'dev.cmd') `
        -Arguments @('build', '-NoRestore', '-SkipTests') `
        -FailureMessage 'Build failed'

    Write-Host 'Creating current local Yak packages...'
    Invoke-RepositoryCommand `
        -Path (Join-Path $repositoryRoot 'dev.cmd') `
        -Arguments @('package', '-SkipBuild') `
        -FailureMessage 'Packaging failed'
}
else {
    Write-Host 'Using existing package artifacts by request.'
}

if (-not (Test-Path -LiteralPath $packageIndexPath -PathType Leaf)) {
    throw "Package index is missing: '$packageIndexPath'. Rerun without -UseExistingPackages."
}
$packageIndex = Get-Content -LiteralPath $packageIndexPath -Raw | ConvertFrom-Json
if ([string] $packageIndex.schema -ne 'goniegonie.dragons-grasshopper.package-index.v1') {
    throw "Unsupported package-index schema: '$($packageIndex.schema)'."
}
$version = [string] $packageIndex.version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Package index has no version.'
}

$installations = @()
foreach ($rhinoHost in $hosts) {
    $yakArtifacts = @($productIds | ForEach-Object {
        Resolve-IndexedYak `
            -PackageIndex $packageIndex `
            -ProductId $_ `
            -PackageTarget ([string] $rhinoHost.packageTarget)
    })

    Write-Host ''
    Write-Host "Removing existing Dragon packages from $($rhinoHost.name)..."
    $installedBefore = @(Invoke-Yak `
        -Executable ([string] $rhinoHost.yak) `
        -Arguments @('list') `
        -LogName ("$($rhinoHost.name.ToLowerInvariant())-list-before.log") `
        -FailureMessage "$($rhinoHost.name) package listing failed")
    foreach ($productId in $productIds) {
        if (Test-PackageListed -Lines $installedBefore -ProductId $productId) {
            $null = Invoke-Yak `
                -Executable ([string] $rhinoHost.yak) `
                -Arguments @('uninstall', $productId) `
                -LogName ("$($rhinoHost.name.ToLowerInvariant())-uninstall-$productId.log") `
                -FailureMessage "$($rhinoHost.name) could not uninstall $productId"
        }
        else {
            Write-Host "$productId is not currently installed in $($rhinoHost.name); continuing."
        }
    }

    $installedAfterRemoval = @(Invoke-Yak `
        -Executable ([string] $rhinoHost.yak) `
        -Arguments @('list') `
        -LogName ("$($rhinoHost.name.ToLowerInvariant())-list-after-removal.log") `
        -FailureMessage "$($rhinoHost.name) post-removal package listing failed")
    foreach ($productId in $productIds) {
        if (Test-PackageListed -Lines $installedAfterRemoval -ProductId $productId) {
            throw "$productId remains installed in $($rhinoHost.name) after uninstall."
        }
    }

    Write-Host "Installing Dragon $version packages into $($rhinoHost.name)..."
    foreach ($artifact in $yakArtifacts) {
        $null = Invoke-Yak `
            -Executable ([string] $rhinoHost.yak) `
            -Arguments @('install', [string] $artifact.path) `
            -LogName ("$($rhinoHost.name.ToLowerInvariant())-install-$($artifact.product).log") `
            -FailureMessage "$($rhinoHost.name) could not install $($artifact.product)"
    }

    $installedFinal = @(Invoke-Yak `
        -Executable ([string] $rhinoHost.yak) `
        -Arguments @('list') `
        -LogName ("$($rhinoHost.name.ToLowerInvariant())-list-final.log") `
        -FailureMessage "$($rhinoHost.name) final package listing failed")
    foreach ($productId in $productIds) {
        if (-not (Test-PackageListed `
                -Lines $installedFinal `
                -ProductId $productId `
                -Version $version)) {
            throw "$productId $version is not listed after installation in $($rhinoHost.name)."
        }
    }

    $installations += [pscustomobject] [ordered] @{
        host = [string] $rhinoHost.name
        rhinoVersion = [string] $rhinoHost.version
        rhinoExecutable = [string] $rhinoHost.rhino
        yakExecutable = [string] $rhinoHost.yak
        pathSource = [string] $rhinoHost.source
        version = $version
        products = @($yakArtifacts | ForEach-Object {
            [pscustomobject] [ordered] @{
                id = [string] $_.product
                artifact = [string] $_.path
                sha256 = [string] $_.sha256
            }
        })
    }
}

$result = [pscustomobject] [ordered] @{
    schema = 'goniegonie.dragons-grasshopper.local-install.v1'
    status = 'installed'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    packageVersion = $version
    rebuiltPackages = -not [bool] $UseExistingPackages
    installations = $installations
}
$resultPath = Join-Path $runRoot 'install-result.json'
Write-Utf8JsonIfChanged -InputObject $result -Path $resultPath -Depth 8

Write-Host ''
Write-Host "Dragon $version installation complete for: $(@($hosts | ForEach-Object { $_.name }) -join ', ')."
Write-Host 'Start Rhino, open Grasshopper, and confirm the InvisibleDragon and SimpleDragon tabs.'
Write-Host "Install log: $resultPath"

#requires -Version 5.1

[CmdletBinding()]
param(
    [switch] $VerifyOnly,
    [switch] $PassThru
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
$lockPath = Join-Path $repositoryRoot 'packaging\yak.lock.json'
$toolsRoot = Join-Path $repositoryRoot '.tools'
$tempRoot = Join-Path $repositoryRoot 'temp'

if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
    throw "Pinned Yak lock not found: '$lockPath'."
}

$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
if ([string] $lock.schema -ne 'goniegonie.tool-lock.v1') {
    throw "Unsupported Yak lock schema in '$lockPath'."
}

$version = [string] $lock.version
$downloadUri = [string] $lock.windows_x64_url
$expectedSize = [long] $lock.windows_x64_size
$expectedSha256 = ([string] $lock.windows_x64_sha256).ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($version) -or
    -not $downloadUri.StartsWith('https://files.mcneel.com/yak/tools/', [System.StringComparison]::OrdinalIgnoreCase) -or
    $expectedSize -le 0 -or
    $expectedSha256 -notmatch '^[0-9a-f]{64}$') {
    throw "The Yak lock is incomplete or does not reference the official McNeel HTTPS tool endpoint."
}

$yakDirectory = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path (Join-Path $toolsRoot (Join-Path 'yak' $version)) `
    -AllowedTopLevelNames @('.tools')
$yakPath = Join-Path $yakDirectory 'yak.exe'

function Test-PinnedYak {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $item = Get-Item -LiteralPath $Path
    if ($item.Length -ne $expectedSize) {
        return $false
    }

    return (Get-Sha256 -Path $Path) -eq $expectedSha256
}

if (-not (Test-PinnedYak -Path $yakPath)) {
    if ($VerifyOnly) {
        throw "Pinned Yak $version is missing or failed size/SHA-256 verification at '$yakPath'."
    }

    $downloadDirectory = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path (Join-Path $tempRoot 'packaging\tools') `
        -AllowedTopLevelNames @('temp')
    Ensure-Directory -Path $downloadDirectory
    $downloadPath = Join-Path $downloadDirectory ("yak-$version-$([Guid]::NewGuid().ToString('N')).download")
    try {
        Write-Host "Downloading pinned Yak $version from the official McNeel endpoint..."
        Invoke-WebRequest -UseBasicParsing -Uri $downloadUri -OutFile $downloadPath
        if (-not (Test-PinnedYak -Path $downloadPath)) {
            $actualSize = (Get-Item -LiteralPath $downloadPath).Length
            $actualSha256 = Get-Sha256 -Path $downloadPath
            throw "Downloaded Yak failed verification (size $actualSize, SHA-256 $actualSha256)."
        }

        Ensure-Directory -Path $yakDirectory
        Move-Item -LiteralPath $downloadPath -Destination $yakPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $downloadPath -PathType Leaf) {
            Remove-Item -LiteralPath $downloadPath -Force
        }
    }
}

if (-not (Test-PinnedYak -Path $yakPath)) {
    throw "Pinned Yak verification failed after acquisition: '$yakPath'."
}

Write-Host "Verified Yak ${version}: $yakPath"
if ($PassThru) {
    Write-Output $yakPath
}

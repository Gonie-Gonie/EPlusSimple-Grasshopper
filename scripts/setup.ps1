#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [string] $EnergyPlusRoot,
    [string] $Rhino7Path,
    [string] $Rhino8Path,
    [switch] $InstallEnergyPlus,
    [switch] $SkipEmbeddedPayloads,
    [switch] $SkipPythonInstall,
    [switch] $SkipPythonEnvironment,
    [switch] $SkipRestore,
    [switch] $RequireEnergyPlus,
    [switch] $RequireRhino7,
    [switch] $RequireRhino8
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
$toolsRoot = Join-Path $repositoryRoot '.tools'
$tempRoot = Join-Path $repositoryRoot 'temp'
$bootstrapRoot = Join-Path $tempRoot 'bootstrap'
$logsRoot = Join-Path $tempRoot 'logs'
$configPath = Join-Path $repositoryRoot '.tools\state\local.settings.json'
$retiredConfigRoot = Join-Path $repositoryRoot '.config'
$retiredConfigPath = Join-Path $retiredConfigRoot 'local.settings.json'
$runtimeManifestPath = Join-Path $repositoryRoot 'resources\runtime\manifest.template.json'
$distributionManifestPath = Join-Path $repositoryRoot 'resources\runtime\distributions.json'
$distributionRoot = Join-Path $toolsRoot 'distributions'
$preparedDistributions = @{}
$pythonEnvironmentRoot = Join-Path $toolsRoot 'venv'
$pythonEnvironmentExecutable = Join-Path $pythonEnvironmentRoot 'Scripts\python.exe'
$pythonEnvironmentStampPath = Join-Path $toolsRoot 'state\python-environment.json'
$pythonRequirementsPath = Join-Path $repositoryRoot 'tools\documentation\requirements.lock.txt'
$pythonEnvironmentVerifierPath = Join-Path $repositoryRoot 'tools\documentation\verify_environment.py'

$globalSettings = Get-Content -LiteralPath (Join-Path $repositoryRoot 'global.json') -Raw | ConvertFrom-Json
$requiredDotNetSdk = [string] $globalSettings.sdk.version
$requiredDotNetRuntime = '8.0.30'
$requiredPython = '3.12.7'
$requiredDocumentationPip = '24.3.1'
$requiredOodocs = '1.3.0'
$requiredEnergyPlusVersion = '24.2.0'
$requiredEnergyPlusBuild = '94a887817b'
$minimumRhino7 = [version] '7.0'
$minimumRhino8 = [version] '8.0'

if ($requiredDotNetSdk -ne '8.0.424') {
    throw "global.json must pin the exact supported SDK 8.0.424; found '$requiredDotNetSdk'."
}

if (-not (Test-Path -LiteralPath $runtimeManifestPath -PathType Leaf)) {
    throw "Pinned runtime manifest not found: '$runtimeManifestPath'."
}

$runtimeManifest = Get-Content -LiteralPath $runtimeManifestPath -Raw | ConvertFrom-Json
if ([string] $runtimeManifest.energyplus_version -ne $requiredEnergyPlusVersion -or
    [string] $runtimeManifest.energyplus_build -ne $requiredEnergyPlusBuild) {
    throw 'resources/runtime/manifest.template.json does not match the pinned EnergyPlus runtime identity.'
}

if (-not (Test-Path -LiteralPath $distributionManifestPath -PathType Leaf)) {
    throw "Pinned distribution manifest not found: '$distributionManifestPath'."
}

$distributionManifest = Get-Content -LiteralPath $distributionManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string] $distributionManifest.schema -ne 'dragons-grasshopper.distributions.v3') {
    throw "Unsupported distribution manifest schema in '$distributionManifestPath'."
}

$expectedDistributions = @{
    'energyplus-24.2.0-windows-x64' = @{
        product = 'invisible-dragon'
        kind = 'energyplus-archive'
        fileName = 'EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip'
        url = 'https://github.com/NREL/EnergyPlus/releases/download/v24.2.0a/EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip'
        size = [int64] 179248139
        sha256 = '26c7c22b731f54031626750284c8b613fb8f03c3aa56b6bc7ec65b6bf8668df1'
        developmentPath = 'energyplus/EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip'
        packagePath = 'runtime/energyplus/EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip'
        licenseEntry = 'EnergyPlus-24.2.0-94a887817b-Windows-x86_64/LICENSE.txt'
        packageLicensePath = 'runtime/energyplus/LICENSE.txt'
        licenseSize = [int64] 3182
        licenseSha256 = 'b43f1553459a4bcc49d180b42123a64a54fcbb6213cd99ac6ac6aa32cb1c1a05'
    }
    'korean-tmy-v1' = @{
        product = 'simple-dragon'
        kind = 'weather-archive'
        fileName = 'KoreanTMY-v1.zip'
        url = 'https://github.com/snu-bslab/EPlusSimple-resources/releases/download/weather/v1/KoreanTMY-v1.zip'
        size = [int64] 128349513
        sha256 = 'fa88b8d69364b6a6b663afdc6dc2eb30c0ddee17cd37e5802ce5a5dec63d92d0'
        developmentPath = 'weather/KoreanTMY-v1.zip'
        packagePath = 'runtime/weather/KoreanTMY-v1.zip'
        originSite = 'https://climate.onebuilding.org/'
        originDataset = 'TMYx'
        originSourcePage = 'https://climate.onebuilding.org/sources/default.html'
        originSouthKoreaIndex = 'https://climate.onebuilding.org/WMO_Region_2_Asia/KOR_South_Korea/index.html'
        originCitation = 'Lawrie, Linda K, Drury B Crawley. 2022. Development of Global Typical Meteorological Years (TMYx). https://climate.onebuilding.org'
        originSolarDataSource = 'ERA5'
        originSolarDataProvider = 'Oikolab'
        originCopernicusLicense = 'https://cds.climate.copernicus.eu/licences/licence-to-use-copernicus-products'
        originOikolabTerms = 'https://docs.oikolab.com/terms/'
        originReviewedAt = '2026-08-31'
        originWeatherRightsVerified = $false
        originWeatherRiskAcceptedByOwner = $true
        originWeatherRiskAcceptanceReview = 'accepted-2026-08-31'
        originWeatherRedistributionStatus = 'owner-risk-accepted-unverified'
    }
}

$distributionPayloads = @($distributionManifest.payloads)
if ($distributionPayloads.Count -ne $expectedDistributions.Count) {
    throw 'resources/runtime/distributions.json must contain exactly the two reviewed embedded payloads.'
}
$seenDistributionIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
foreach ($payload in $distributionPayloads) {
    $id = [string] $payload.id
    if (-not $expectedDistributions.ContainsKey($id) -or -not $seenDistributionIds.Add($id)) {
        throw "Unreviewed distribution payload '$id' is present in resources/runtime/distributions.json."
    }
    $expected = $expectedDistributions[$id]
    $uri = [uri] ([string] $payload.url)
    if ($uri.Scheme -ne 'https' -or
        [string] $payload.product -ne [string] $expected.product -or
        [string] $payload.kind -ne [string] $expected.kind -or
        [string] $payload.fileName -ne [string] $expected.fileName -or
        [string] $payload.url -ne [string] $expected.url -or
        [int64] $payload.size -ne [int64] $expected.size -or
        ([string] $payload.sha256).ToLowerInvariant() -ne [string] $expected.sha256 -or
        [string] $payload.developmentPath -ne [string] $expected.developmentPath -or
        [string] $payload.packagePath -ne [string] $expected.packagePath -or
        ($id -eq 'korean-tmy-v1' -and (
            [string] $payload.origin.site -ne [string] $expected.originSite -or
            [string] $payload.origin.dataset -ne [string] $expected.originDataset -or
            [string] $payload.origin.sourcePage -ne [string] $expected.originSourcePage -or
            [string] $payload.origin.southKoreaIndex -ne [string] $expected.originSouthKoreaIndex -or
            [string] $payload.origin.citation -ne [string] $expected.originCitation -or
            [string] $payload.origin.solarDataSource -ne [string] $expected.originSolarDataSource -or
            [string] $payload.origin.solarDataProvider -ne [string] $expected.originSolarDataProvider -or
            [string] $payload.origin.copernicusLicense -ne [string] $expected.originCopernicusLicense -or
            [string] $payload.origin.oikolabTerms -ne [string] $expected.originOikolabTerms -or
            [string] $payload.origin.reviewedAt -ne [string] $expected.originReviewedAt -or
            [bool] $payload.origin.weatherRightsVerified -ne [bool] $expected.originWeatherRightsVerified -or
            [bool] $payload.origin.weatherRiskAcceptedByOwner -ne [bool] $expected.originWeatherRiskAcceptedByOwner -or
            [string] $payload.origin.weatherRiskAcceptanceReview -ne [string] $expected.originWeatherRiskAcceptanceReview -or
            [string] $payload.origin.weatherRedistributionStatus -ne [string] $expected.originWeatherRedistributionStatus)) -or
        ($id -eq 'energyplus-24.2.0-windows-x64' -and (
            [string] $payload.licenseEntry -ne [string] $expected.licenseEntry -or
            [string] $payload.packageLicensePath -ne [string] $expected.packageLicensePath -or
            [int64] $payload.licenseSize -ne [int64] $expected.licenseSize -or
            ([string] $payload.licenseSha256).ToLowerInvariant() -ne [string] $expected.licenseSha256))) {
        throw "Distribution payload '$id' differs from the reviewed HTTPS identity/product/path contract."
    }
}

# Windows PowerShell 5.1 otherwise negotiates an obsolete TLS version on some
# clean machines. TLS 1.2 is supported by all official download endpoints used.
[System.Net.ServicePointManager]::SecurityProtocol =
    [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12

function Add-UniqueCandidate {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.ArrayList] $List,

        [AllowNull()]
        [AllowEmptyString()]
        [string] $Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    $fullPath = $Path
    try {
        $fullPath = [System.IO.Path]::GetFullPath($Path)
    }
    catch {
        return
    }

    foreach ($existing in $List) {
        if ($existing.Equals($fullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            return
        }
    }

    $null = $List.Add($fullPath)
}

function Remove-SetupOwnedTree {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $safePath = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $Path `
        -AllowedTopLevelNames @('temp', '.tools')
    Assert-NoReparsePoints -Path $safePath -AnchorPath $repositoryRoot

    if ($WhatIfPreference) {
        Write-Host "What if: remove setup-owned tree '$safePath'."
        return
    }

    Remove-Item -LiteralPath $safePath -Recurse -Force
}

function Remove-RetiredRootLocalSettings {
    if (-not (Test-Path -LiteralPath $retiredConfigPath -PathType Leaf)) {
        return
    }

    $safeConfigPath = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $retiredConfigPath `
        -AllowedTopLevelNames @('.config')
    Assert-NoReparsePoints -Path $safeConfigPath -AnchorPath $repositoryRoot

    $remainingEntries = @(Get-ChildItem -LiteralPath $retiredConfigRoot -Force |
        Where-Object { -not $_.FullName.Equals(
            $safeConfigPath,
            [System.StringComparison]::OrdinalIgnoreCase) })

    if ($WhatIfPreference) {
        Write-Host "What if: remove retired generated configuration '$safeConfigPath'."
        if ($remainingEntries.Count -eq 0) {
            Write-Host "What if: remove empty retired configuration directory '$retiredConfigRoot'."
        }
        return
    }

    Remove-Item -LiteralPath $safeConfigPath -Force
    if ($remainingEntries.Count -eq 0) {
        Remove-Item -LiteralPath $retiredConfigRoot -Force
    }
}

function Invoke-OfficialDownload {
    param(
        [Parameter(Mandatory = $true)]
        [uri] $Uri,

        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    Ensure-Directory -Path (Split-Path -Parent $Destination)
    Write-Host "Downloading official payload: $Uri"
    if ($WhatIfPreference) {
        Write-Host "What if: download to '$Destination'."
        return
    }

    $partial = $Destination + '.partial'
    if (Test-Path -LiteralPath $partial) {
        Remove-Item -LiteralPath $partial -Force
    }

    Invoke-WebRequest -Uri $Uri -OutFile $partial -UseBasicParsing
    Move-Item -LiteralPath $partial -Destination $Destination -Force
}

function Get-DistributionPayload {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Product
    )

    $matches = @($distributionPayloads | Where-Object { [string] $_.product -eq $Product })
    if ($matches.Count -ne 1) {
        throw "Expected exactly one distribution payload for '$Product'; found $($matches.Count)."
    }
    return $matches[0]
}

function Get-DistributionArchivePath {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Payload
    )

    return Join-Path $distributionRoot (([string] $Payload.developmentPath).Replace('/', '\'))
}

function Assert-SafeDistributionZip {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [object] $Payload
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
        $fileEntries = @()
        [int64] $expandedBytes = 0
        foreach ($entry in @($archive.Entries)) {
            $name = ([string] $entry.FullName).Replace('\', '/')
            $parts = @($name.Split('/') | Where-Object { $_ -ne '' })
            if ([string]::IsNullOrWhiteSpace($name) -or
                $name.StartsWith('/') -or
                $name -match '^[A-Za-z]:' -or
                [System.IO.Path]::IsPathRooted($name) -or
                $parts -contains '..') {
                throw "Unsafe ZIP entry '$name' in '$Path'."
            }
            if (-not $seen.Add($name)) {
                throw "Case-insensitive duplicate ZIP entry '$name' in '$Path'."
            }
            $unixType = (($entry.ExternalAttributes -shr 16) -band 0xF000)
            if ($unixType -eq 0xA000) {
                throw "Symbolic-link ZIP entry '$name' is not allowed in '$Path'."
            }
            if (-not [string]::IsNullOrEmpty($entry.Name)) {
                $fileEntries += $entry
                $expandedBytes += [int64] $entry.Length
            }
        }
        if ($archive.Entries.Count -gt 20000 -or $expandedBytes -gt [int64] 8589934592) {
            throw "ZIP safety limits were exceeded by '$Path'."
        }

        if ([string] $Payload.kind -eq 'weather-archive') {
            $epwEntries = @($fileEntries | Where-Object {
                ([string] $_.FullName).Replace('\', '/') -notmatch '/' -and
                [System.IO.Path]::GetExtension([string] $_.Name).Equals('.epw', [System.StringComparison]::OrdinalIgnoreCase)
            })
            if ($fileEntries.Count -ne [int] $Payload.archiveEpwCount -or
                $epwEntries.Count -ne [int] $Payload.archiveEpwCount) {
                throw "Weather archive must contain exactly $($Payload.archiveEpwCount) root EPW files and nothing else."
            }

            $metadataPath = Join-Path $repositoryRoot (([string] $Payload.metadataPath).Replace('/', '\'))
            if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
                throw "Weather metadata is missing: '$metadataPath'."
            }
            $metadataColumn = [string] $Payload.metadataColumn
            $metadataNames = @(Import-Csv -LiteralPath $metadataPath -Encoding UTF8 |
                ForEach-Object { [string] $_.$metadataColumn } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                Sort-Object -Unique)
            if ($metadataNames.Count -ne [int] $Payload.metadataReferencedUniqueEpwCount) {
                throw "Weather metadata references $($metadataNames.Count) unique EPWs; expected $($Payload.metadataReferencedUniqueEpwCount)."
            }
            $archiveNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($entry in $epwEntries) {
                $null = $archiveNames.Add([string] $entry.Name)
            }
            foreach ($metadataName in $metadataNames) {
                if (-not $archiveNames.Contains($metadataName)) {
                    throw "Weather metadata references missing archive entry '$metadataName'."
                }
            }
        }
        elseif ([string] $Payload.kind -eq 'energyplus-archive') {
            $licenseEntry = @($fileEntries | Where-Object {
                ([string] $_.FullName).Replace('\', '/') -ceq [string] $Payload.licenseEntry
            })
            if ($licenseEntry.Count -ne 1 -or
                [int64] $licenseEntry[0].Length -ne [int64] $Payload.licenseSize) {
                throw "EnergyPlus archive license entry is missing or has the wrong size in '$Path'."
            }
            $stream = $licenseEntry[0].Open()
            try {
                $sha = [System.Security.Cryptography.SHA256]::Create()
                try {
                    $licenseHash = ([System.BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
                }
                finally {
                    $sha.Dispose()
                }
            }
            finally {
                $stream.Dispose()
            }
            if ($licenseHash -ne ([string] $Payload.licenseSha256).ToLowerInvariant()) {
                throw "EnergyPlus archive license SHA-256 mismatch in '$Path'."
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-DistributionArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [object] $Payload
    )

    $item = Get-Item -LiteralPath $Path
    if ([int64] $item.Length -ne [int64] $Payload.size) {
        throw "Distribution size mismatch for '$Path': expected $($Payload.size), found $($item.Length)."
    }
    $hash = (Get-Sha256 -Path $Path).ToLowerInvariant()
    if ($hash -ne ([string] $Payload.sha256).ToLowerInvariant()) {
        throw "Distribution SHA-256 mismatch for '$Path'."
    }
    Assert-SafeDistributionZip -Path $Path -Payload $Payload
}

function Ensure-DistributionPayload {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Payload
    )

    $id = [string] $Payload.id
    if ($preparedDistributions.ContainsKey($id)) {
        return $preparedDistributions[$id]
    }

    $archivePath = Get-DistributionArchivePath -Payload $Payload
    $archiveReady = $false
    if (Test-Path -LiteralPath $archivePath -PathType Leaf) {
        try {
            Assert-DistributionArchive -Path $archivePath -Payload $Payload
            $archiveReady = $true
            Write-Host "Verified cached distribution: $archivePath"
        }
        catch {
            Write-Warning "Cached distribution is invalid and will be replaced: $($_.Exception.Message)"
            if ($WhatIfPreference) {
                Write-Host "What if: remove invalid cached distribution '$archivePath'."
            }
            else {
                Remove-Item -LiteralPath $archivePath -Force
            }
        }
    }
    if (-not $archiveReady -and $WhatIfPreference) {
        Write-Host "What if: download and verify $($Payload.url) to '$archivePath'."
    }
    elseif (-not $archiveReady) {
        Ensure-Directory -Path (Split-Path -Parent $archivePath)
        $partial = $archivePath + '.partial'
        if (Test-Path -LiteralPath $partial) {
            Remove-Item -LiteralPath $partial -Force
        }
        try {
            Write-Host "Downloading pinned embedded payload: $($Payload.url)"
            Invoke-WebRequest -Uri ([uri] ([string] $Payload.url)) -OutFile $partial -UseBasicParsing
            Assert-DistributionArchive -Path $partial -Payload $Payload
            Move-Item -LiteralPath $partial -Destination $archivePath -Force
        }
        finally {
            if (Test-Path -LiteralPath $partial) {
                Remove-Item -LiteralPath $partial -Force
            }
        }
        Write-Host "Prepared verified distribution: $archivePath"
    }

    $result = [pscustomobject] [ordered] @{
        id = $id
        product = [string] $Payload.product
        status = if ($WhatIfPreference -and -not $archiveReady) { 'planned' } else { 'ready' }
        path = $archivePath
        size = [int64] $Payload.size
        sha256 = ([string] $Payload.sha256).ToLowerInvariant()
        packagePath = [string] $Payload.packagePath
    }
    $preparedDistributions[$id] = $result
    return $result
}

function Get-DotNetSdkSelection {
    $candidates = New-Object System.Collections.ArrayList
    Add-UniqueCandidate -List $candidates -Path (Join-Path $toolsRoot 'dotnet\dotnet.exe')

    if (-not [string]::IsNullOrWhiteSpace($env:DOTNET_ROOT)) {
        Add-UniqueCandidate -List $candidates -Path (Join-Path $env:DOTNET_ROOT 'dotnet.exe')
    }

    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    if (-not [string]::IsNullOrWhiteSpace($programFiles)) {
        Add-UniqueCandidate -List $candidates -Path (Join-Path $programFiles 'dotnet\dotnet.exe')
    }

    $programFilesX86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        Add-UniqueCandidate -List $candidates -Path (Join-Path $programFilesX86 'dotnet\dotnet.exe')
    }

    foreach ($command in @(Get-Command dotnet.exe -All -ErrorAction SilentlyContinue)) {
        Add-UniqueCandidate -List $candidates -Path $command.Source
    }

    foreach ($candidate in $candidates) {
        $candidate = Resolve-ExecutablePathWithRepositorySafety `
            -RepositoryRoot $repositoryRoot `
            -ExecutablePath $candidate
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            continue
        }

        $previousErrorActionPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $sdkLines = @(& $candidate --list-sdks 2>$null)
            $listSdksExitCode = $LASTEXITCODE
            $versionLines = @(& $candidate --version 2>$null)
            $versionExitCode = $LASTEXITCODE
            $runtimeLines = @(& $candidate --list-runtimes 2>$null)
            $runtimeExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        if ($listSdksExitCode -ne 0 -or $versionExitCode -ne 0 -or $runtimeExitCode -ne 0) {
            continue
        }

        $exactSdk = $sdkLines | Where-Object { $_ -match ('^' + [regex]::Escape($requiredDotNetSdk) + '\s+\[') } | Select-Object -First 1
        $exactRuntime = $runtimeLines | Where-Object { $_ -match ('^Microsoft\.NETCore\.App\s+' + [regex]::Escape($requiredDotNetRuntime) + '\s+\[') } | Select-Object -First 1
        if ($null -ne $exactSdk -and
            $null -ne $exactRuntime -and
            $versionLines.Count -gt 0 -and
            [string] $versionLines[-1] -eq $requiredDotNetSdk) {
            $source = 'system'
            if ($candidate.StartsWith($toolsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                $source = 'repository-local'
            }

            return [pscustomobject] [ordered] @{
                status = 'ready'
                sdkVersion = $requiredDotNetSdk
                executable = $candidate
                root = Split-Path -Parent $candidate
                source = $source
            }
        }
    }

    return $null
}

function Install-PinnedDotNetSdk {
    $localDotNetRoot = Join-Path $toolsRoot 'dotnet'
    $archiveName = 'dotnet-sdk-8.0.424-win-x64.zip'
    $archivePath = Join-Path $bootstrapRoot $archiveName
    $staging = Join-Path $bootstrapRoot 'dotnet-sdk-8.0.424-extracted'
    $downloadUri = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.424/dotnet-sdk-8.0.424-win-x64.zip'
    # Published in Microsoft's .NET 8 release metadata:
    # https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/8.0/releases.json
    $expectedSha512 = '1787ab90635c2950672ed7c6507b000e1b212ea7d9a22fcef37061344d37c64d4c4eda12b8742601eff5b45c8736485b31c55613892f240c300190e4e88a58b0'

    $downloadRequired = -not (Test-Path -LiteralPath $archivePath -PathType Leaf)
    if (-not $downloadRequired) {
        $downloadRequired = (Get-Sha512 -Path $archivePath) -ne $expectedSha512
    }

    if ($downloadRequired) {
        Invoke-OfficialDownload -Uri $downloadUri -Destination $archivePath
    }

    if ($WhatIfPreference) {
        Write-Host "What if: SHA-512 verify and fully extract .NET SDK $requiredDotNetSdk into '$localDotNetRoot'."
        return
    }

    $actualSha512 = Get-Sha512 -Path $archivePath
    if ($actualSha512 -ne $expectedSha512) {
        throw "The official .NET SDK archive SHA-512 did not match release metadata. Expected $expectedSha512; got $actualSha512."
    }

    Remove-SetupOwnedTree -Path $staging
    Ensure-Directory -Path $staging

    # The Windows inbox bsdtar handles this large SDK archive more reliably
    # than the dotnet-install.ps1 extraction path on Windows PowerShell 5.1.
    $tar = Get-Command tar.exe -ErrorAction SilentlyContinue
    if ($null -ne $tar) {
        Invoke-LoggedNativeCommand `
            -FilePath $tar.Source `
            -ArgumentList @('-xf', $archivePath, '-C', $staging) `
            -LogPath (Join-Path $logsRoot 'setup-dotnet-extract.log') `
            -FailureMessage 'Failed to extract the verified .NET SDK archive'
    }
    else {
        Expand-Archive -LiteralPath $archivePath -DestinationPath $staging -Force
    }

    $requiredFiles = @(
        (Join-Path $staging 'dotnet.exe'),
        (Join-Path $staging "sdk\$requiredDotNetSdk\dotnet.dll"),
        (Join-Path $staging "host\fxr\$requiredDotNetRuntime\hostfxr.dll"),
        (Join-Path $staging "shared\Microsoft.NETCore.App\$requiredDotNetRuntime\System.Private.CoreLib.dll")
    )
    foreach ($requiredFile in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Verified SDK extraction is incomplete; required file is missing: '$requiredFile'."
        }
    }

    $stagedDotNet = Join-Path $staging 'dotnet.exe'
    $stagedDotNet = Resolve-ExecutablePathWithRepositorySafety `
        -RepositoryRoot $repositoryRoot `
        -ExecutablePath $stagedDotNet `
        -AllowedRepositoryTopLevelNames @('temp')
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $stagedSdkOutput = @(& $stagedDotNet --version 2>$null)
        $stagedSdkExitCode = $LASTEXITCODE
        $stagedRuntimeOutput = @(& $stagedDotNet --list-runtimes 2>$null)
        $stagedRuntimeExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($stagedSdkExitCode -ne 0 -or $stagedSdkOutput.Count -eq 0 -or [string] $stagedSdkOutput[-1] -ne $requiredDotNetSdk) {
        throw "Extracted SDK failed self-check for exact version $requiredDotNetSdk."
    }
    if ($stagedRuntimeExitCode -ne 0 -or
        -not ($stagedRuntimeOutput | Where-Object { $_ -match ('^Microsoft\.NETCore\.App\s+' + [regex]::Escape($requiredDotNetRuntime) + '\s+\[') })) {
        throw "Extracted SDK is missing Microsoft.NETCore.App $requiredDotNetRuntime."
    }

    Remove-SetupOwnedTree -Path $localDotNetRoot
    Ensure-Directory -Path (Split-Path -Parent $localDotNetRoot)
    Move-Item -LiteralPath $staging -Destination $localDotNetRoot

    $installedDotNet = Join-Path $localDotNetRoot 'dotnet.exe'
    $installedDotNet = Resolve-ExecutablePathWithRepositorySafety `
        -RepositoryRoot $repositoryRoot `
        -ExecutablePath $installedDotNet
    $installedVersion = @(& $installedDotNet --version 2>$null)
    if ($LASTEXITCODE -ne 0 -or $installedVersion.Count -eq 0 -or [string] $installedVersion[-1] -ne $requiredDotNetSdk) {
        throw 'The completed repository-local .NET SDK failed its final self-check.'
    }
}

function Get-PythonDetails {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Executable
    )

    $Executable = Resolve-ExecutablePathWithRepositorySafety `
        -RepositoryRoot $repositoryRoot `
        -ExecutablePath $Executable `
        -AllowedRepositoryTopLevelNames @('.tools', 'temp')
    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
        return $null
    }

    # Windows Store aliases are launchers, not reproducible interpreter paths.
    if ($Executable -match '\\WindowsApps\\') {
        return $null
    }

    # Avoid quote-sensitive JSON source in a native-command argument; the
    # separator cannot occur in a Windows executable path.
    $pythonCode = "import ensurepip,struct,sys,venv; assert sys.prefix == sys.base_prefix and struct.calcsize('P') == 8; print('%d.%d.%d|%s' % (sys.version_info[0],sys.version_info[1],sys.version_info[2],sys.executable))"
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $Executable -c $pythonCode 2>$null)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0 -or $output.Count -eq 0) {
        return $null
    }

    $identityParts = @(([string] $output[-1]) -split '\|', 2)
    if ($identityParts.Count -ne 2) {
        return $null
    }

    if ([string] $identityParts[0] -ne $requiredPython) {
        return $null
    }

    $resolvedExecutable = [System.IO.Path]::GetFullPath([string] $identityParts[1])
    $source = 'system'
    if ($resolvedExecutable.StartsWith($toolsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $source = 'repository-local'
    }

    return [pscustomobject] [ordered] @{
        status = 'ready'
        version = $requiredPython
        executable = $resolvedExecutable
        source = $source
    }
}

function Get-PythonSelection {
    $candidates = New-Object System.Collections.ArrayList
    Add-UniqueCandidate -List $candidates -Path (Join-Path $toolsRoot 'python\3.12.7\python.exe')

    foreach ($commandName in @('python.exe', 'python3.exe')) {
        foreach ($command in @(Get-Command $commandName -All -ErrorAction SilentlyContinue)) {
            Add-UniqueCandidate -List $candidates -Path $command.Source
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        Add-UniqueCandidate -List $candidates -Path (Join-Path $env:LOCALAPPDATA 'Programs\Python\Python312\python.exe')
    }

    foreach ($base in @(
        [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles),
        [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
    )) {
        if (-not [string]::IsNullOrWhiteSpace($base)) {
            Add-UniqueCandidate -List $candidates -Path (Join-Path $base 'Python312\python.exe')
        }
    }

    $pythonLauncher = Get-Command py.exe -ErrorAction SilentlyContinue
    if ($null -ne $pythonLauncher) {
        $previousErrorActionPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $launcherPath = @(& $pythonLauncher.Source -3.12 -c 'import sys; print(sys.executable)' 2>$null)
            $launcherExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        if ($launcherExitCode -eq 0 -and $launcherPath.Count -gt 0) {
            Add-UniqueCandidate -List $candidates -Path $launcherPath[-1]
        }
    }

    foreach ($candidate in $candidates) {
        $details = Get-PythonDetails -Executable $candidate
        if ($null -ne $details) {
            return $details
        }
    }

    return $null
}

function Install-PinnedPython {
    # The python.org embeddable distribution intentionally omits venv and pip.
    # The CPython-owned NuGet package is the official build-oriented layout and
    # contains the standard library, venv, and ensurepip required by setup.
    $pythonArchive = Join-Path $bootstrapRoot 'python.3.12.7.nupkg.zip'
    $expectedArchiveSize = [int64] 14428078
    $expectedArchiveSha256 = '149dd298e0b7a82250ca019471770fff079874088a4e8501ca20922d7df3a6ac'
    $pythonDownload = 'https://api.nuget.org/v3-flatcontainer/python/3.12.7/python.3.12.7.nupkg'
    $target = Join-Path $toolsRoot 'python\3.12.7'
    $staging = Join-Path $bootstrapRoot 'python-3.12.7-nuget-extracted'

    $downloadRequired = -not (Test-Path -LiteralPath $pythonArchive -PathType Leaf)
    if (-not $downloadRequired) {
        $downloadRequired = (Get-Sha256 -Path $pythonArchive) -ne $expectedArchiveSha256
    }

    if ($downloadRequired) {
        Invoke-OfficialDownload -Uri $pythonDownload -Destination $pythonArchive
    }

    if ($WhatIfPreference) {
        Write-Host "What if: verify and extract Python $requiredPython into '$target'."
        return
    }

    $actualArchiveSha256 = Get-Sha256 -Path $pythonArchive
    if ($actualArchiveSha256 -ne $expectedArchiveSha256) {
        throw "Python archive SHA-256 mismatch. Expected $expectedArchiveSha256; got $actualArchiveSha256."
    }
    $actualArchiveSize = (Get-Item -LiteralPath $pythonArchive).Length
    if ($actualArchiveSize -ne $expectedArchiveSize) {
        throw "Python archive size mismatch. Expected $expectedArchiveSize; got $actualArchiveSize."
    }

    Remove-SetupOwnedTree -Path $staging
    Ensure-Directory -Path $staging
    Expand-Archive -LiteralPath $pythonArchive -DestinationPath $staging -Force

    $stagedRuntime = Join-Path $staging 'tools'
    $stagedPython = Join-Path $stagedRuntime 'python.exe'
    $stagedDetails = Get-PythonDetails -Executable $stagedPython
    if ($null -eq $stagedDetails) {
        throw 'The extracted official Python package is not a venv-capable 64-bit CPython 3.12.7 runtime.'
    }

    Remove-SetupOwnedTree -Path $target
    Ensure-Directory -Path (Split-Path -Parent $target)
    Move-Item -LiteralPath $stagedRuntime -Destination $target
    Remove-SetupOwnedTree -Path $staging
}

function Set-DocumentationPythonProcessEnvironment {
    $env:PYTHONHOME = $null
    $env:PYTHONPATH = $null
    $env:PYTHONUTF8 = '1'
    $env:PYTHONNOUSERSITE = '1'
    $env:PIP_NO_INPUT = '1'
    $env:PIP_DISABLE_PIP_VERSION_CHECK = '1'
    $env:PIP_REQUIRE_VIRTUALENV = '1'
}

function Test-PythonEnvironmentReady {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Python,

        [Parameter(Mandatory = $true)]
        [string] $RequirementsSha256
    )

    if (-not (Test-Path -LiteralPath $pythonEnvironmentExecutable -PathType Leaf) -or
        -not (Test-Path -LiteralPath $pythonEnvironmentStampPath -PathType Leaf)) {
        return $false
    }
    $safeEnvironmentExecutable = Resolve-ExecutablePathWithRepositorySafety `
        -RepositoryRoot $repositoryRoot `
        -ExecutablePath $pythonEnvironmentExecutable
    Assert-NoReparsePoints -Path $pythonEnvironmentStampPath -AnchorPath $repositoryRoot

    try {
        $stamp = Get-Content -LiteralPath $pythonEnvironmentStampPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ([string] $stamp.schema -ne 'dragons.documentation-python-environment.v1' -or
            [string] $stamp.pythonVersion -ne $requiredPython -or
            [string] $stamp.baseExecutable -ne [string] $Python.executable -or
            [string] $stamp.venvExecutable -ne $pythonEnvironmentExecutable -or
            [string] $stamp.requirementsSha256 -ne $RequirementsSha256 -or
            [string] $stamp.pipVersion -ne $requiredDocumentationPip -or
            [string] $stamp.oodocsVersion -ne $requiredOodocs) {
            return $false
        }
    }
    catch {
        return $false
    }

    Set-DocumentationPythonProcessEnvironment
    $verificationArguments = @(
        '-I', '-B', '-X', 'utf8',
        $pythonEnvironmentVerifierPath,
        '--requirements', $pythonRequirementsPath,
        '--expected-python', $requiredPython,
        '--expected-oodocs', $requiredOodocs
    )
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $safeEnvironmentExecutable @verificationArguments 2>$null | Out-Null
        $verificationExitCode = $LASTEXITCODE
    }
    catch {
        return $false
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return $verificationExitCode -eq 0
}

function Remove-PythonEnvironmentStamp {
    if (-not (Test-Path -LiteralPath $pythonEnvironmentStampPath -PathType Leaf)) {
        return
    }

    $safeStamp = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $pythonEnvironmentStampPath `
        -AllowedTopLevelNames @('.tools')
    Assert-NoReparsePoints -Path $safeStamp -AnchorPath $repositoryRoot
    if ($WhatIfPreference) {
        Write-Host "What if: remove stale documentation environment stamp '$safeStamp'."
        return
    }
    Remove-Item -LiteralPath $safeStamp -Force
}

function Ensure-PythonEnvironment {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Python
    )

    if (-not (Test-Path -LiteralPath $pythonRequirementsPath -PathType Leaf)) {
        throw "Documentation dependency lock not found: '$pythonRequirementsPath'."
    }
    if (-not (Test-Path -LiteralPath $pythonEnvironmentVerifierPath -PathType Leaf)) {
        throw "Documentation environment verifier not found: '$pythonEnvironmentVerifierPath'."
    }

    $requirementsSha256 = Get-Sha256 -Path $pythonRequirementsPath
    if ($SkipPythonEnvironment) {
        Write-Host 'Repository Python environment skipped by -SkipPythonEnvironment.'
        return [pscustomobject] [ordered] @{
            status = 'skipped'
            version = $requiredPython
            root = $pythonEnvironmentRoot
            executable = $pythonEnvironmentExecutable
            baseExecutable = $Python.executable
            requirementsSha256 = $requirementsSha256
            pipVersion = $requiredDocumentationPip
            oodocsVersion = $requiredOodocs
            reason = 'Repository Python environment preparation was explicitly skipped.'
        }
    }

    if ([string] $Python.status -eq 'planned') {
        Write-Host "What if: create and verify the repository Python environment at '$pythonEnvironmentRoot'."
        return [pscustomobject] [ordered] @{
            status = 'planned'
            version = $requiredPython
            root = $pythonEnvironmentRoot
            executable = $pythonEnvironmentExecutable
            baseExecutable = $Python.executable
            requirementsSha256 = $requirementsSha256
            pipVersion = $requiredDocumentationPip
            oodocsVersion = $requiredOodocs
        }
    }

    if ([string] $Python.status -ne 'ready') {
        $reason = "A venv-capable Python $requiredPython base interpreter is unavailable. Rerun setup without -SkipPythonInstall."
        Write-Warning $reason
        return [pscustomobject] [ordered] @{
            status = 'missing'
            version = $requiredPython
            root = $pythonEnvironmentRoot
            executable = $pythonEnvironmentExecutable
            baseExecutable = $null
            requirementsSha256 = $requirementsSha256
            pipVersion = $requiredDocumentationPip
            oodocsVersion = $requiredOodocs
            reason = $reason
        }
    }
    if ([string] $Python.source -cne 'repository-local') {
        throw (
            "The documentation venv requires the hash-pinned repository CPython. " +
            "Rerun setup without -SkipPythonInstall, or also pass -SkipPythonEnvironment " +
            "when only non-documentation workflows are required.")
    }

    if (Test-PythonEnvironmentReady -Python $Python -RequirementsSha256 $requirementsSha256) {
        Write-Host "Repository Python environment: ready ($pythonEnvironmentRoot)"
        return [pscustomobject] [ordered] @{
            status = 'ready'
            version = $requiredPython
            root = $pythonEnvironmentRoot
            executable = $pythonEnvironmentExecutable
            baseExecutable = $Python.executable
            requirementsSha256 = $requirementsSha256
            pipVersion = $requiredDocumentationPip
            oodocsVersion = $requiredOodocs
        }
    }

    if ($WhatIfPreference) {
        Write-Host "What if: replace and verify the repository Python environment at '$pythonEnvironmentRoot'."
        return [pscustomobject] [ordered] @{
            status = 'planned'
            version = $requiredPython
            root = $pythonEnvironmentRoot
            executable = $pythonEnvironmentExecutable
            baseExecutable = $Python.executable
            requirementsSha256 = $requirementsSha256
            pipVersion = $requiredDocumentationPip
            oodocsVersion = $requiredOodocs
        }
    }

    $setupPythonTemp = Join-Path $tempRoot 'setup\python-environment'
    $pipCache = Join-Path $setupPythonTemp 'pip-cache'
    $smokePdf = Join-Path $setupPythonTemp 'oodocs-smoke.pdf'
    # Validate the existing ancestor chain before creating or writing through
    # it, then inspect the small setup-owned subtree after creation.
    Assert-NoReparsePoints `
        -Path (Join-Path $setupPythonTemp '.dragons-ancestor-safety-probe') `
        -AnchorPath $repositoryRoot
    Ensure-Directory -Path $setupPythonTemp
    Ensure-Directory -Path $pipCache
    Assert-NoReparsePoints -Path $setupPythonTemp -AnchorPath $repositoryRoot
    Set-DocumentationPythonProcessEnvironment

    try {
        Remove-PythonEnvironmentStamp
        Remove-SetupOwnedTree -Path $pythonEnvironmentRoot

        Invoke-LoggedNativeCommand `
            -FilePath ([string] $Python.executable) `
            -ArgumentList @(
                '-I', '-B', '-X', 'utf8',
                '-m', 'venv', '--copies', $pythonEnvironmentRoot
            ) `
            -LogPath (Join-Path $logsRoot 'python-environment-create.log') `
            -FailureMessage 'Creating the repository Python environment failed'

        $safeEnvironmentExecutable = Resolve-ExecutablePathWithRepositorySafety `
            -RepositoryRoot $repositoryRoot `
            -ExecutablePath $pythonEnvironmentExecutable

        Invoke-LoggedNativeCommand `
            -FilePath $safeEnvironmentExecutable `
            -ArgumentList @(
                '-I', '-B', '-X', 'utf8',
                '-m', 'pip', '--isolated', 'install',
                '--disable-pip-version-check',
                '--no-input',
                '--index-url', 'https://pypi.org/simple',
                '--require-hashes',
                '--only-binary=:all:',
                '--no-deps',
                '--requirement', $pythonRequirementsPath,
                '--cache-dir', $pipCache
            ) `
            -LogPath (Join-Path $logsRoot 'python-environment-install.log') `
            -FailureMessage 'Installing the hash-locked documentation dependencies failed'

        Invoke-LoggedNativeCommand `
            -FilePath $safeEnvironmentExecutable `
            -ArgumentList @('-I', '-B', '-X', 'utf8', '-m', 'pip', 'check') `
            -LogPath (Join-Path $logsRoot 'python-environment-pip-check.log') `
            -FailureMessage 'The repository Python environment has inconsistent dependencies'

        Invoke-LoggedNativeCommand `
            -FilePath $safeEnvironmentExecutable `
            -ArgumentList @(
                '-I', '-B', '-X', 'utf8',
                $pythonEnvironmentVerifierPath,
                '--requirements', $pythonRequirementsPath,
                '--expected-python', $requiredPython,
                '--expected-oodocs', $requiredOodocs,
                '--smoke-output', $smokePdf
            ) `
            -LogPath (Join-Path $logsRoot 'python-environment-verify.log') `
            -FailureMessage 'Verifying the exact OODocs environment and PDF renderer failed'

        $stamp = [ordered] @{
            schema = 'dragons.documentation-python-environment.v1'
            pythonVersion = $requiredPython
            baseExecutable = [string] $Python.executable
            venvExecutable = $pythonEnvironmentExecutable
            requirementsSha256 = $requirementsSha256
            pipVersion = $requiredDocumentationPip
            oodocsVersion = $requiredOodocs
        }
        Assert-NoReparsePoints `
            -Path (Join-Path (Split-Path -Parent $pythonEnvironmentStampPath) '.dragons-ancestor-safety-probe') `
            -AnchorPath $repositoryRoot
        Write-Utf8JsonIfChanged -InputObject $stamp -Path $pythonEnvironmentStampPath -Depth 4

        if (Test-Path -LiteralPath $smokePdf -PathType Leaf) {
            Assert-NoReparsePoints -Path $setupPythonTemp -AnchorPath $repositoryRoot
            Remove-Item -LiteralPath $smokePdf -Force
        }
        Remove-SetupOwnedTree -Path $pipCache
    }
    catch {
        Remove-PythonEnvironmentStamp
        Remove-SetupOwnedTree -Path $pythonEnvironmentRoot
        throw
    }

    Write-Host "Repository Python environment: created ($pythonEnvironmentRoot)"
    return [pscustomobject] [ordered] @{
        status = 'ready'
        version = $requiredPython
        root = $pythonEnvironmentRoot
        executable = $pythonEnvironmentExecutable
        baseExecutable = $Python.executable
        requirementsSha256 = $requirementsSha256
        pipVersion = $requiredDocumentationPip
        oodocsVersion = $requiredOodocs
    }
}

function Get-EnergyPlusDetails {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root
    )

    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        return $null
    }

    $resolvedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $executable = Join-Path $resolvedRoot 'energyplus.exe'
    $idd = Join-Path $resolvedRoot 'Energy+.idd'
    $epJsonSchema = Join-Path $resolvedRoot 'Energy+.schema.epJSON'
    $expandObjects = Join-Path $resolvedRoot 'ExpandObjects.exe'

    foreach ($requiredFile in @($executable, $idd, $epJsonSchema, $expandObjects)) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            return $null
        }
    }

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $versionOutput = (@(& $executable --version 2>&1) -join ' ').Trim()
        $versionExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($versionExitCode -ne 0 -or
        $versionOutput -notmatch ('Version\s+' + [regex]::Escape($requiredEnergyPlusVersion) + '-' + [regex]::Escape($requiredEnergyPlusBuild))) {
        Write-Warning "Ignoring EnergyPlus candidate '$resolvedRoot': expected $requiredEnergyPlusVersion-$requiredEnergyPlusBuild, reported '$versionOutput'."
        return $null
    }

    $executableHash = Get-Sha256 -Path $executable
    $iddHash = Get-Sha256 -Path $idd
    $epJsonSchemaHash = Get-Sha256 -Path $epJsonSchema
    $expandObjectsHash = Get-Sha256 -Path $expandObjects

    if ($executableHash -ne ([string] $runtimeManifest.energyplus_exe_sha256).ToLowerInvariant() -or
        $iddHash -ne ([string] $runtimeManifest.energyplus_idd_sha256).ToLowerInvariant() -or
        $epJsonSchemaHash -ne ([string] $runtimeManifest.energyplus_epjson_schema_sha256).ToLowerInvariant() -or
        $expandObjectsHash -ne ([string] $runtimeManifest.expandobjects_sha256).ToLowerInvariant()) {
        Write-Warning "Ignoring EnergyPlus candidate '$resolvedRoot': one or more pinned runtime hashes do not match."
        return $null
    }

    $source = 'system'
    if ($resolvedRoot.StartsWith($toolsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $source = 'repository-local'
    }

    return [pscustomobject] [ordered] @{
        status = 'ready'
        version = $requiredEnergyPlusVersion
        build = $requiredEnergyPlusBuild
        root = $resolvedRoot
        executable = $executable
        idd = $idd
        epJsonSchema = $epJsonSchema
        source = $source
        hashes = [ordered] @{
            energyplusExeSha256 = $executableHash
            energyPlusIddSha256 = $iddHash
            energyPlusEpJsonSchemaSha256 = $epJsonSchemaHash
            expandObjectsSha256 = $expandObjectsHash
        }
    }
}

function Get-EnergyPlusSelection {
    $candidates = New-Object System.Collections.ArrayList
    Add-UniqueCandidate -List $candidates -Path $EnergyPlusRoot
    Add-UniqueCandidate -List $candidates -Path (Join-Path $toolsRoot 'energyplus\24.2.0-94a887817b')
    Add-UniqueCandidate -List $candidates -Path 'C:\EnergyPlusV24-2-0'

    if (-not [string]::IsNullOrWhiteSpace($env:ENERGYPLUS_ROOT)) {
        Add-UniqueCandidate -List $candidates -Path $env:ENERGYPLUS_ROOT
    }

    foreach ($candidate in $candidates) {
        $details = Get-EnergyPlusDetails -Root $candidate
        if ($null -ne $details) {
            return $details
        }
    }

    return $null
}

function Install-PinnedEnergyPlus {
    $payload = Get-DistributionPayload -Product 'invisible-dragon'
    $prepared = Ensure-DistributionPayload -Payload $payload
    $archivePath = [string] $prepared.path
    $staging = Join-Path $bootstrapRoot 'energyplus-24.2.0-extracted'
    $target = Join-Path $toolsRoot 'energyplus\24.2.0-94a887817b'

    if ($WhatIfPreference) {
        Write-Host "What if: extract and verify EnergyPlus $requiredEnergyPlusVersion-$requiredEnergyPlusBuild into '$target'."
        return
    }

    Remove-SetupOwnedTree -Path $staging
    Ensure-Directory -Path $staging
    Expand-Archive -LiteralPath $archivePath -DestinationPath $staging -Force

    $runtimeRoot = $null
    foreach ($candidateExecutable in @(Get-ChildItem -LiteralPath $staging -Filter 'energyplus.exe' -File -Recurse)) {
        $candidateRoot = Split-Path -Parent $candidateExecutable.FullName
        $details = Get-EnergyPlusDetails -Root $candidateRoot
        if ($null -ne $details) {
            $runtimeRoot = $candidateRoot
            break
        }
    }

    if ($null -eq $runtimeRoot) {
        throw 'The official EnergyPlus archive did not contain the pinned, hash-matching Windows runtime.'
    }

    Remove-SetupOwnedTree -Path $target
    Ensure-Directory -Path (Split-Path -Parent $target)
    Move-Item -LiteralPath $runtimeRoot -Destination $target
    Remove-SetupOwnedTree -Path $staging
}

function Get-RhinoDetails {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Executable,

        [Parameter(Mandatory = $true)]
        [version] $MinimumVersion,

        [Parameter(Mandatory = $true)]
        [string] $ProductName
    )

    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
        return $null
    }

    $resolved = [System.IO.Path]::GetFullPath($Executable)
    $item = Get-Item -LiteralPath $resolved
    $rawVersion = [string] $item.VersionInfo.FileVersion
    $versionMatch = [regex]::Match($rawVersion, '\d+(?:\.\d+){1,3}')
    if (-not $versionMatch.Success) {
        return [pscustomobject] [ordered] @{
            status = 'incompatible'
            minimumVersion = $MinimumVersion.ToString(2)
            version = $rawVersion
            executable = $resolved
            reason = 'The executable version could not be parsed.'
        }
    }

    $actualVersion = [version] $versionMatch.Value
    if ($actualVersion -lt $MinimumVersion) {
        return [pscustomobject] [ordered] @{
            status = 'incompatible'
            minimumVersion = $MinimumVersion.ToString(2)
            version = $actualVersion.ToString()
            executable = $resolved
            reason = "$ProductName $($MinimumVersion.ToString(2)) or newer is required for its target."
        }
    }

    return [pscustomobject] [ordered] @{
        status = 'ready'
        minimumVersion = $MinimumVersion.ToString(2)
        version = $actualVersion.ToString()
        executable = $resolved
        root = Split-Path -Parent (Split-Path -Parent $resolved)
    }
}

function Get-RhinoSelection {
    param(
        [Parameter(Mandatory = $true)]
        [int] $MajorVersion,

        [Parameter(Mandatory = $true)]
        [version] $MinimumVersion,

        [AllowNull()]
        [AllowEmptyString()]
        [string] $ExplicitPath
    )

    $candidates = New-Object System.Collections.ArrayList
    Add-UniqueCandidate -List $candidates -Path $ExplicitPath
    Add-UniqueCandidate -List $candidates -Path ("C:\Program Files\Rhino $MajorVersion\System\Rhino.exe")

    foreach ($registryPath in @(
        "HKLM:\SOFTWARE\McNeel\Rhinoceros\$MajorVersion.0\Install",
        "HKLM:\SOFTWARE\WOW6432Node\McNeel\Rhinoceros\$MajorVersion.0\Install"
    )) {
        $properties = Get-ItemProperty -LiteralPath $registryPath -ErrorAction SilentlyContinue
        if ($null -eq $properties) {
            continue
        }

        foreach ($propertyName in @('InstallPath', 'Path', 'InstallFolder')) {
            $property = $properties.PSObject.Properties[$propertyName]
            if ($null -ne $property -and -not [string]::IsNullOrWhiteSpace([string] $property.Value)) {
                Add-UniqueCandidate -List $candidates -Path (Join-Path ([string] $property.Value) 'System\Rhino.exe')
                Add-UniqueCandidate -List $candidates -Path (Join-Path ([string] $property.Value) 'Rhino.exe')
            }
        }
    }

    $firstIncompatible = $null
    foreach ($candidate in $candidates) {
        $details = Get-RhinoDetails `
            -Executable $candidate `
            -MinimumVersion $MinimumVersion `
            -ProductName "Rhino $MajorVersion"
        if ($null -eq $details) {
            continue
        }

        if ($details.status -eq 'ready') {
            return $details
        }

        if ($null -eq $firstIncompatible) {
            $firstIncompatible = $details
        }
    }

    if ($null -ne $firstIncompatible) {
        return $firstIncompatible
    }

    return [pscustomobject] [ordered] @{
        status = 'missing'
        minimumVersion = $MinimumVersion.ToString(2)
        version = $null
        executable = $null
        reason = "Rhino $MajorVersion is not installed. Headless compile remains available."
    }
}

Write-Host "Repository: $repositoryRoot"
Write-Host 'Embedded payload policy: cache and verify the two reviewed archives by default; pass -SkipEmbeddedPayloads to opt out.'
Write-Host 'EnergyPlus extraction policy: detect and verify only; pass -InstallEnergyPlus to extract the verified official archive.'
Write-Host 'Rhino policy: detect Rhino 7 and Rhino 8 independently; never install licensed Rhino software.'

foreach ($directory in @($toolsRoot, $tempRoot, $bootstrapRoot, $logsRoot)) {
    Ensure-Directory -Path $directory
}

if ($SkipEmbeddedPayloads) {
    Write-Host 'Embedded payload preparation skipped by -SkipEmbeddedPayloads.'
}
else {
    foreach ($payload in $distributionPayloads) {
        $null = Ensure-DistributionPayload -Payload $payload
    }
}

$dotnet = Get-DotNetSdkSelection
if ($null -eq $dotnet) {
    Write-Host "Exact .NET SDK $requiredDotNetSdk was not found."
    Install-PinnedDotNetSdk
    if ($WhatIfPreference) {
        $dotnet = [pscustomobject] [ordered] @{
            status = 'planned'
            sdkVersion = $requiredDotNetSdk
            executable = Join-Path $toolsRoot 'dotnet\dotnet.exe'
            root = Join-Path $toolsRoot 'dotnet'
            source = 'repository-local'
        }
    }
    else {
        $dotnet = Get-DotNetSdkSelection
        if ($null -eq $dotnet) {
            throw "The .NET installer completed but exact SDK $requiredDotNetSdk is still unavailable."
        }
    }
}
Write-Host ".NET SDK: $($dotnet.sdkVersion) [$($dotnet.source)]"

$repositoryPythonExecutable = Join-Path $toolsRoot 'python\3.12.7\python.exe'
$python = $null
if ($SkipPythonInstall) {
    $python = Get-PythonSelection
}
else {
    $python = Get-PythonDetails -Executable $repositoryPythonExecutable
    if ($null -eq $python) {
        Write-Host "Hash-pinned repository Python $requiredPython was not found."
        Install-PinnedPython
        if ($WhatIfPreference) {
            $python = [pscustomobject] [ordered] @{
                status = 'planned'
                version = $requiredPython
                executable = $repositoryPythonExecutable
                source = 'repository-local'
            }
        }
        else {
            $python = Get-PythonDetails -Executable $repositoryPythonExecutable
            if ($null -eq $python) {
                throw 'The verified repository Python package failed its final identity check.'
            }
        }
    }
}

if ($null -eq $python) {
    $python = [pscustomobject] [ordered] @{
        status = 'missing'
        version = $requiredPython
        executable = $null
        source = $null
        reason = 'Exact Python oracle is unavailable; rerun setup without -SkipPythonInstall.'
    }
    Write-Warning $python.reason
}
else {
    Write-Host "Python oracle: $($python.version) [$($python.source)]"
}

$pythonEnvironment = Ensure-PythonEnvironment -Python $python

$energyPlus = Get-EnergyPlusSelection
if ($null -eq $energyPlus -and $InstallEnergyPlus) {
    Write-Host "Pinned EnergyPlus $requiredEnergyPlusVersion-$requiredEnergyPlusBuild was not found."
    Install-PinnedEnergyPlus
    if (-not $WhatIfPreference) {
        $energyPlus = Get-EnergyPlusSelection
    }
}

if ($null -eq $energyPlus) {
    $energyPlus = [pscustomobject] [ordered] @{
        status = 'missing'
        version = $requiredEnergyPlusVersion
        build = $requiredEnergyPlusBuild
        root = $null
        executable = $null
        idd = $null
        epJsonSchema = $null
        source = $null
        hashes = $null
        reason = "No hash-matching runtime was found. Use 'dev.cmd setup -InstallEnergyPlus' to install the official portable ZIP."
    }

    if ($RequireEnergyPlus -and -not $WhatIfPreference) {
        throw $energyPlus.reason
    }
    Write-Warning $energyPlus.reason
}
else {
    Write-Host "EnergyPlus: $($energyPlus.version)-$($energyPlus.build) [$($energyPlus.source)]"
}

$rhino7 = Get-RhinoSelection -MajorVersion 7 -MinimumVersion $minimumRhino7 -ExplicitPath $Rhino7Path
$rhino8 = Get-RhinoSelection -MajorVersion 8 -MinimumVersion $minimumRhino8 -ExplicitPath $Rhino8Path

foreach ($rhinoResult in @($rhino7, $rhino8)) {
    if ($rhinoResult.status -eq 'ready') {
        Write-Host "Rhino detected: $($rhinoResult.version) at $($rhinoResult.executable)"
    }
    else {
        Write-Warning $rhinoResult.reason
    }
}

if ($RequireRhino7 -and $rhino7.status -ne 'ready' -and -not $WhatIfPreference) {
    throw "Rhino 7 was required but its status is '$($rhino7.status)'."
}
if ($RequireRhino8 -and $rhino8.status -ne 'ready' -and -not $WhatIfPreference) {
    throw "Rhino 8.0+ was required but its status is '$($rhino8.status)'."
}

$localSettings = [ordered] @{
    schema = 'dragons-grasshopper.local-settings.v1'
    repositoryRoot = $repositoryRoot
    dotnet = $dotnet
    pythonOracle = $python
    pythonEnvironment = $pythonEnvironment
    energyPlus = $energyPlus
    distributions = @($preparedDistributions.Values | Sort-Object product)
    rhino = [ordered] @{
        rhino7 = $rhino7
        rhino8 = $rhino8
    }
    paths = [ordered] @{
        tools = $toolsRoot
        temp = $tempRoot
        artifacts = Join-Path $repositoryRoot 'artifacts'
        nugetPackages = Join-Path $toolsRoot 'nuget\packages'
        nugetHttpCache = Join-Path $toolsRoot 'nuget\http-cache'
        dotnetCliHome = Join-Path $toolsRoot 'dotnet-cli-home'
        pythonEnvironment = $pythonEnvironmentRoot
        buildOutput = Join-Path $tempRoot 'build'
        testResults = Join-Path $tempRoot 'test-results'
        logs = $logsRoot
        documentationOutput = Join-Path $repositoryRoot 'artifacts\documentation'
    }
}

Assert-NoReparsePoints `
    -Path (Join-Path (Split-Path -Parent $configPath) '.dragons-ancestor-safety-probe') `
    -AnchorPath $repositoryRoot
Write-Utf8JsonIfChanged -InputObject $localSettings -Path $configPath -Depth 10
Remove-RetiredRootLocalSettings

$solution = Find-SolutionFile -RepositoryRoot $repositoryRoot
if ($null -eq $solution) {
    Write-Warning 'No solution file exists yet. Toolchain detection completed; dependency restore was skipped.'
}
elseif ($SkipRestore) {
    Write-Host 'Dependency restore skipped by -SkipRestore.'
}
elseif ($dotnet.status -eq 'planned') {
    Write-Host 'What if: restore solution dependencies after installing the exact SDK.'
}
else {
    Set-RepositoryBuildEnvironment -RepositoryRoot $repositoryRoot -DotNetExecutable $dotnet.executable
    Invoke-WithTrackedPackageLockNormalization `
        -RepositoryRoot $repositoryRoot `
        -Action {
            Invoke-LoggedNativeCommand `
                -FilePath $dotnet.executable `
                -ArgumentList @(
                    'restore', $solution,
                    '--configfile', (Join-Path $repositoryRoot 'NuGet.config'),
                    '--packages', (Join-Path $toolsRoot 'nuget\packages'),
                    '--nologo'
                ) `
                -LogPath (Join-Path $logsRoot 'setup-restore.log') `
                -FailureMessage 'Dependency restore failed during setup'
        }
}

Write-Host ''
Write-Host 'Setup complete.'
Write-Host "Local configuration: $configPath"
if ($rhino7.status -ne 'ready' -or $rhino8.status -ne 'ready') {
    Write-Host 'Headless builds remain enabled. Re-run setup after installing either Rhino version to activate that version''s load/geometry tests.'
}

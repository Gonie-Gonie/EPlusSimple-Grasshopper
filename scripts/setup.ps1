#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [string] $EnergyPlusRoot,
    [string] $Rhino7Path,
    [string] $Rhino8Path,
    [switch] $InstallEnergyPlus,
    [switch] $SkipPythonInstall,
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
$configPath = Join-Path $repositoryRoot '.config\local.settings.json'
$runtimeManifestPath = Join-Path $repositoryRoot 'runtime\manifest.template.json'

$globalSettings = Get-Content -LiteralPath (Join-Path $repositoryRoot 'global.json') -Raw | ConvertFrom-Json
$requiredDotNetSdk = [string] $globalSettings.sdk.version
$requiredDotNetRuntime = '8.0.30'
$requiredPython = '3.12.7'
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
    throw 'runtime/manifest.template.json does not match the pinned EnergyPlus runtime identity.'
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
    Assert-NoReparsePoints -Path $safePath

    if ($WhatIfPreference) {
        Write-Host "What if: remove setup-owned tree '$safePath'."
        return
    }

    Remove-Item -LiteralPath $safePath -Recurse -Force
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

    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
        return $null
    }

    # Windows Store aliases are launchers, not reproducible interpreter paths.
    if ($Executable -match '\\WindowsApps\\') {
        return $null
    }

    # Avoid quote-sensitive JSON source in a native-command argument; the
    # separator cannot occur in a Windows executable path.
    $pythonCode = "import sys; print('%d.%d.%d|%s' % (sys.version_info[0],sys.version_info[1],sys.version_info[2],sys.executable))"
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
        $source = 'repository-embedded'
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
    $pythonArchive = Join-Path $bootstrapRoot 'python-3.12.7-embed-amd64.zip'
    $expectedArchiveSha256 = '0d57bb6cb078b74d23dbfe91f77d6780d45bed328911609f1f7ee2ba1606bf44'
    $pythonDownload = 'https://www.python.org/ftp/python/3.12.7/python-3.12.7-embed-amd64.zip'
    $target = Join-Path $toolsRoot 'python\3.12.7'
    $staging = Join-Path $bootstrapRoot 'python-3.12.7-extracted'

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

    Remove-SetupOwnedTree -Path $staging
    Ensure-Directory -Path $staging
    Expand-Archive -LiteralPath $pythonArchive -DestinationPath $staging -Force

    $stagedPython = Join-Path $staging 'python.exe'
    $stagedDetails = Get-PythonDetails -Executable $stagedPython
    if ($null -eq $stagedDetails) {
        throw 'The extracted official Python archive did not identify itself as Python 3.12.7.'
    }

    Remove-SetupOwnedTree -Path $target
    Ensure-Directory -Path (Split-Path -Parent $target)
    Move-Item -LiteralPath $staging -Destination $target
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
    $expandObjects = Join-Path $resolvedRoot 'ExpandObjects.exe'

    foreach ($requiredFile in @($executable, $idd, $expandObjects)) {
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
    $expandObjectsHash = Get-Sha256 -Path $expandObjects

    if ($executableHash -ne ([string] $runtimeManifest.energyplus_exe_sha256).ToLowerInvariant() -or
        $iddHash -ne ([string] $runtimeManifest.energyplus_idd_sha256).ToLowerInvariant() -or
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
        source = $source
        hashes = [ordered] @{
            energyplusExeSha256 = $executableHash
            energyPlusIddSha256 = $iddHash
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
    # v24.2.0a is the corrected 24.2.0 release whose build identity is
    # 94a887817b. The NREL URL redirects to the project's current official
    # GitHub organization if the organization name changes.
    $archiveName = 'EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip'
    $downloadUri = 'https://github.com/NREL/EnergyPlus/releases/download/v24.2.0a/' + $archiveName
    $archivePath = Join-Path $bootstrapRoot $archiveName
    $staging = Join-Path $bootstrapRoot 'energyplus-24.2.0-extracted'
    $target = Join-Path $toolsRoot 'energyplus\24.2.0-94a887817b'

    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        Invoke-OfficialDownload -Uri $downloadUri -Destination $archivePath
    }

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
Write-Host 'EnergyPlus default policy: detect and verify only; pass -InstallEnergyPlus to download the official portable ZIP.'
Write-Host 'Rhino policy: detect Rhino 7 and Rhino 8 independently; never install licensed Rhino software.'

foreach ($directory in @($toolsRoot, $tempRoot, $bootstrapRoot, $logsRoot)) {
    Ensure-Directory -Path $directory
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

$python = Get-PythonSelection
if ($null -eq $python -and -not $SkipPythonInstall) {
    Write-Host "Exact Python oracle $requiredPython was not found."
    Install-PinnedPython
    if ($WhatIfPreference) {
        $python = [pscustomobject] [ordered] @{
            status = 'planned'
            version = $requiredPython
            executable = Join-Path $toolsRoot 'python\3.12.7\python.exe'
            source = 'repository-embedded'
        }
    }
    else {
        $python = Get-PythonSelection
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
        source = $null
        hashes = $null
        reason = 'No hash-matching runtime was found. Use setup.cmd -InstallEnergyPlus to install the official portable ZIP.'
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
    schema = 'goniegonie.dragons-grasshopper.local-settings.v1'
    repositoryRoot = $repositoryRoot
    dotnet = $dotnet
    pythonOracle = $python
    energyPlus = $energyPlus
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
        buildOutput = Join-Path $tempRoot 'build'
        testResults = Join-Path $tempRoot 'test-results'
        logs = $logsRoot
    }
}

Write-Utf8JsonIfChanged -InputObject $localSettings -Path $configPath -Depth 10

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

Write-Host ''
Write-Host 'Setup complete.'
Write-Host "Local configuration: $configPath"
if ($rhino7.status -ne 'ready' -or $rhino8.status -ne 'ready') {
    Write-Host 'Headless builds remain enabled. Re-run setup after installing either Rhino version to activate that version''s load/geometry tests.'
}

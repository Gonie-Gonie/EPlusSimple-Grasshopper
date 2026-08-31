@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "DRAGONS_INSTALLER_FILE=%~f0"
set "DRAGONS_INSTALLER_ROOT=%~dp0"
set "DRAGONS_INSTALLER_ARG1=%~1"
set "DRAGONS_INSTALLER_ARG2=%~2"
set "DRAGONS_INSTALLER_ARG3=%~3"

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop'; try { $source=[IO.File]::ReadAllText($env:DRAGONS_INSTALLER_FILE); $marker=([char]58)+'__DRAGONS_POWERSHELL_BEGIN__'; $offset=$source.LastIndexOf($marker,[StringComparison]::Ordinal); if($offset -lt 0){throw 'Embedded installer body is missing.'}; & ([ScriptBlock]::Create($source.Substring($offset+$marker.Length))) } catch { [Console]::Error.WriteLine('ERROR: '+$_.Exception.Message); exit 1 }"
set "DRAGONS_INSTALLER_EXIT=%ERRORLEVEL%"

endlocal & exit /b %DRAGONS_INSTALLER_EXIT%

:__DRAGONS_POWERSHELL_BEGIN__

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$ExpectedSchema = 'goniegonie.dragons-grasshopper.windows-installer.v1'
$ExpectedVersion = '0.1.0'
$ProductDefinitions = @(
    [pscustomobject] [ordered] @{
        id = 'invisible-dragon'
        displayName = 'InvisibleDragon'
    },
    [pscustomobject] [ordered] @{
        id = 'simple-dragon'
        displayName = 'SimpleDragon'
    }
)
$TargetDefinitions = @(
    [pscustomobject] [ordered] @{
        id = 'rhino7'
        name = 'Rhino7'
        label = 'Rhino 7'
        majorVersion = 7
        standardExecutable = 'C:\Program Files\Rhino 7\System\Rhino.exe'
    },
    [pscustomobject] [ordered] @{
        id = 'rhino8'
        name = 'Rhino8'
        label = 'Rhino 8'
        majorVersion = 8
        standardExecutable = 'C:\Program Files\Rhino 8\System\Rhino.exe'
    }
)

function Show-InstallerUsage {
    Write-Host @'
Dragons for Grasshopper installer

Usage:
  Install-Dragons.cmd [all|rhino7|rhino8] [--check]

Examples:
  Install-Dragons.cmd
  Install-Dragons.cmd --check
  Install-Dragons.cmd rhino7
  Install-Dragons.cmd --check rhino8

The default target is all installed Rhino 7 and Rhino 8 generations. --check
verifies the complete bundle and installed Rhino/Yak tools without changing
packages. Close every Rhino process before either check or installation.
'@
}

function Get-OptionalProperty {
    param(
        [AllowNull()]
        [object] $InputObject,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Test-PathWithin {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Candidate
    )

    $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $candidatePath = [System.IO.Path]::GetFullPath($Candidate)
    return $candidatePath.StartsWith(
        $rootPath + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoBundleReparsePoint {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BundleRoot,

        [Parameter(Mandatory = $true)]
        [string] $Candidate
    )

    $rootPath = [System.IO.Path]::GetFullPath($BundleRoot).TrimEnd('\', '/')
    $candidatePath = [System.IO.Path]::GetFullPath($Candidate).TrimEnd('\', '/')
    if (-not $candidatePath.Equals(
            $rootPath,
            [System.StringComparison]::OrdinalIgnoreCase) -and
        -not (Test-PathWithin -Root $rootPath -Candidate $candidatePath)) {
        throw "Bundle path escaped the extracted root: '$candidatePath'."
    }

    $currentPath = $candidatePath
    while ($true) {
        if (Test-Path -LiteralPath $currentPath) {
            $item = Get-Item -LiteralPath $currentPath -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Bundle paths may not traverse reparse point '$currentPath'."
            }
        }

        if ($currentPath.Equals(
                $rootPath,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }

        $parentPath = Split-Path -Parent $currentPath
        if ([string]::IsNullOrWhiteSpace($parentPath) -or
            $parentPath.Equals(
                $currentPath,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Could not reach bundle root '$rootPath' from '$candidatePath'."
        }
        $currentPath = $parentPath.TrimEnd('\', '/')
    }
}

function Resolve-BundleChildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BundleRoot,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        $RelativePath.Contains('\') -or
        $RelativePath.StartsWith('/') -or
        $RelativePath -match '^[A-Za-z]:' -or
        $RelativePath.Contains('://')) {
        throw "$Label has an invalid bundle-relative path '$RelativePath'."
    }

    $segments = @($RelativePath.Split('/'))
    if ($segments.Count -eq 0 -or
        @($segments | Where-Object {
            [string]::IsNullOrWhiteSpace($_) -or $_ -eq '.' -or $_ -eq '..'
        }).Count -ne 0) {
        throw "$Label has an unsafe path segment in '$RelativePath'."
    }

    $candidatePath = [System.IO.Path]::GetFullPath((
        Join-Path $BundleRoot ($RelativePath -replace '/', '\')
    ))
    if (-not (Test-PathWithin -Root $BundleRoot -Candidate $candidatePath)) {
        throw "$Label escaped the extracted bundle: '$candidatePath'."
    }
    Assert-NoBundleReparsePoint -BundleRoot $BundleRoot -Candidate $candidatePath

    return $candidatePath
}

function Test-JsonInteger {
    param(
        [AllowNull()]
        [object] $Value
    )

    return $Value -is [byte] -or
        $Value -is [sbyte] -or
        $Value -is [int16] -or
        $Value -is [uint16] -or
        $Value -is [int32] -or
        $Value -is [uint32] -or
        $Value -is [int64] -or
        $Value -is [uint64]
}

function Read-AndVerifyReleaseManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BundleRoot
    )

    $manifestPath = Resolve-BundleChildPath `
        -BundleRoot $BundleRoot `
        -RelativePath 'release-manifest.json' `
        -Label 'Release manifest'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Release manifest is missing beside the installer: '$manifestPath'."
    }

    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 |
            ConvertFrom-Json
    }
    catch {
        throw "Release manifest is not valid JSON: '$manifestPath'. $($_.Exception.Message)"
    }

    $schema = [string] (Get-OptionalProperty -InputObject $manifest -Name 'schema')
    $version = [string] (Get-OptionalProperty -InputObject $manifest -Name 'version')
    if ($schema -cne $ExpectedSchema) {
        throw "Release manifest schema '$schema' is not '$ExpectedSchema'."
    }
    if ($version -cne $ExpectedVersion) {
        throw "Release manifest version '$version' is not '$ExpectedVersion'."
    }

    $products = @(Get-OptionalProperty -InputObject $manifest -Name 'products')
    if ($products.Count -ne $ProductDefinitions.Count) {
        throw "Release manifest must contain exactly $($ProductDefinitions.Count) products."
    }

    $verifiedPackages = New-Object 'System.Collections.Generic.List[object]'
    for ($productIndex = 0; $productIndex -lt $ProductDefinitions.Count; $productIndex++) {
        $expectedProduct = $ProductDefinitions[$productIndex]
        $product = $products[$productIndex]
        $productId = [string] (Get-OptionalProperty -InputObject $product -Name 'id')
        $displayName = [string] (Get-OptionalProperty -InputObject $product -Name 'displayName')
        if ($productId -cne [string] $expectedProduct.id -or
            $displayName -cne [string] $expectedProduct.displayName) {
            throw (
                "Release manifest product $productIndex must be " +
                "'$($expectedProduct.id)'/'$($expectedProduct.displayName)'.")
        }

        $packages = @(Get-OptionalProperty -InputObject $product -Name 'packages')
        if ($packages.Count -ne $TargetDefinitions.Count) {
            throw "Product '$productId' must contain exactly two Rhino package records."
        }

        for ($targetIndex = 0; $targetIndex -lt $TargetDefinitions.Count; $targetIndex++) {
            $expectedTarget = $TargetDefinitions[$targetIndex]
            $package = $packages[$targetIndex]
            $target = [string] (Get-OptionalProperty -InputObject $package -Name 'target')
            $relativePath = [string] (Get-OptionalProperty -InputObject $package -Name 'path')
            $bytesValue = Get-OptionalProperty -InputObject $package -Name 'bytes'
            $sha256 = [string] (Get-OptionalProperty -InputObject $package -Name 'sha256')

            $major = [int] $expectedTarget.majorVersion
            $expectedFileName = '{0}-{1}-rh{2}-win.yak' -f `
                $productId,
                $ExpectedVersion,
                $major
            $expectedRelativePath = 'packages/{0}/{1}' -f `
                [string] $expectedTarget.id,
                $expectedFileName
            $label = "$displayName $($expectedTarget.label) package"

            if ($target -cne [string] $expectedTarget.id -or
                $relativePath -cne $expectedRelativePath) {
                throw (
                    "$label must use target '$($expectedTarget.id)' and path " +
                    "'$expectedRelativePath'.")
            }
            if (-not (Test-JsonInteger -Value $bytesValue) -or [int64] $bytesValue -le 0) {
                throw "$label has an invalid byte length."
            }
            if ($sha256 -cnotmatch '^[0-9a-f]{64}$') {
                throw "$label has an invalid lowercase SHA-256 value."
            }

            $packagePath = Resolve-BundleChildPath `
                -BundleRoot $BundleRoot `
                -RelativePath $relativePath `
                -Label $label
            if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
                throw "$label is missing: '$packagePath'."
            }

            $item = Get-Item -LiteralPath $packagePath
            if ($item.Length -ne [int64] $bytesValue) {
                throw (
                    "$label byte length mismatch. Expected $bytesValue; " +
                    "found $($item.Length).")
            }
            $actualSha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actualSha256 -cne $sha256) {
                throw (
                    "$label SHA-256 mismatch. Expected $sha256; " +
                    "found $actualSha256.")
            }

            $verifiedPackages.Add([pscustomobject] [ordered] @{
                product = $productId
                displayName = $displayName
                target = $target
                majorVersion = $major
                relativePath = $relativePath
                path = $packagePath
                bytes = [int64] $bytesValue
                sha256 = $sha256
            })
        }
    }

    if ($verifiedPackages.Count -ne 4) {
        throw 'Release manifest did not resolve the exact four required Yak packages.'
    }

    return [pscustomobject] [ordered] @{
        path = $manifestPath
        version = $version
        packages = $verifiedPackages.ToArray()
    }
}

function Verify-BundleChecksumInventory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BundleRoot,

        [Parameter(Mandatory = $true)]
        [object] $Release
    )

    $checksumPath = Resolve-BundleChildPath `
        -BundleRoot $BundleRoot `
        -RelativePath 'checksums.sha256' `
        -Label 'Bundle checksum inventory'
    if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
        throw "Bundle checksum inventory is missing: '$checksumPath'."
    }

    $rawText = Get-Content -LiteralPath $checksumPath -Raw -Encoding UTF8
    $normalizedText = $rawText -replace "`r`n", "`n"
    if ($normalizedText.Contains("`r")) {
        throw 'Bundle checksum inventory contains unsupported line endings.'
    }
    $trimmedText = $normalizedText.TrimEnd("`n")
    if ([string]::IsNullOrWhiteSpace($trimmedText)) {
        throw 'Bundle checksum inventory is empty.'
    }

    $records = New-Object 'System.Collections.Generic.List[object]'
    foreach ($line in @($trimmedText.Split("`n"))) {
        $match = [regex]::Match($line, '^(?<sha256>[0-9a-f]{64})  (?<path>[^\s].*)$')
        if (-not $match.Success) {
            throw "Bundle checksum inventory has an invalid line: '$line'."
        }
        $records.Add([pscustomobject] [ordered] @{
            sha256 = $match.Groups['sha256'].Value
            path = $match.Groups['path'].Value
        })
    }

    $expectedPaths = @(
        'Install-Dragons.cmd',
        'LICENSE.txt',
        'NOTICE.md',
        'README.txt',
        'release-manifest.json'
    ) + @($Release.packages | ForEach-Object { [string] $_.relativePath })
    if ($records.Count -ne $expectedPaths.Count) {
        throw (
            'Bundle checksum inventory must contain exactly ' +
            "$($expectedPaths.Count) records; found $($records.Count).")
    }

    foreach ($expectedPath in $expectedPaths) {
        $matches = @($records | Where-Object { [string] $_.path -ceq $expectedPath })
        if ($matches.Count -ne 1) {
            throw "Bundle checksum inventory must contain '$expectedPath' exactly once."
        }

        $contentPath = Resolve-BundleChildPath `
            -BundleRoot $BundleRoot `
            -RelativePath $expectedPath `
            -Label "Checksummed bundle file '$expectedPath'"
        if (-not (Test-Path -LiteralPath $contentPath -PathType Leaf)) {
            throw "Checksummed bundle file is missing: '$contentPath'."
        }

        $verifiedPackage = @($Release.packages | Where-Object {
            [string] $_.relativePath -ceq $expectedPath
        })
        $actualSha256 = if ($verifiedPackage.Count -eq 1) {
            # Package bytes were already hashed during the manifest preflight.
            [string] $verifiedPackage[0].sha256
        }
        elseif ($verifiedPackage.Count -eq 0) {
            (Get-FileHash -LiteralPath $contentPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        else {
            throw "Bundle package path '$expectedPath' is duplicated."
        }

        if ([string] $matches[0].sha256 -cne $actualSha256) {
            throw (
                "Bundle checksum mismatch for '$expectedPath'. Expected " +
                "$($matches[0].sha256); found $actualSha256.")
        }
    }
}

function Add-UniqueRhinoCandidate {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[object]] $Candidates,

        [AllowNull()]
        [AllowEmptyString()]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Source
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    try {
        $fullPath = [System.IO.Path]::GetFullPath($Path)
    }
    catch {
        return
    }

    if (@($Candidates | Where-Object {
        ([string] $_.path).Equals($fullPath, [System.StringComparison]::OrdinalIgnoreCase)
    }).Count -eq 0) {
        $Candidates.Add([pscustomobject] [ordered] @{
            path = $fullPath
            source = $Source
        })
    }
}

function Get-RhinoCandidates {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Definition
    )

    $candidates = New-Object 'System.Collections.Generic.List[object]'
    Add-UniqueRhinoCandidate `
        -Candidates $candidates `
        -Path ([string] $Definition.standardExecutable) `
        -Source 'standard-location'

    $major = [int] $Definition.majorVersion
    foreach ($registryPath in @(
        "HKLM:\SOFTWARE\McNeel\Rhinoceros\$major.0\Install",
        "HKLM:\SOFTWARE\WOW6432Node\McNeel\Rhinoceros\$major.0\Install"
    )) {
        $properties = Get-ItemProperty -LiteralPath $registryPath -ErrorAction SilentlyContinue
        if ($null -eq $properties) {
            continue
        }

        foreach ($propertyName in @('InstallPath', 'Path', 'InstallFolder')) {
            $property = $properties.PSObject.Properties[$propertyName]
            if ($null -eq $property -or
                [string]::IsNullOrWhiteSpace([string] $property.Value)) {
                continue
            }

            $registeredPath = [string] $property.Value
            $trimmedPath = $registeredPath.TrimEnd('\', '/')
            $leafName = [System.IO.Path]::GetFileName($trimmedPath)
            if ($leafName.Equals('Rhino.exe', [System.StringComparison]::OrdinalIgnoreCase)) {
                Add-UniqueRhinoCandidate `
                    -Candidates $candidates `
                    -Path $trimmedPath `
                    -Source "registry:$registryPath/$propertyName"
            }
            elseif ($leafName.Equals('System', [System.StringComparison]::OrdinalIgnoreCase)) {
                Add-UniqueRhinoCandidate `
                    -Candidates $candidates `
                    -Path (Join-Path $trimmedPath 'Rhino.exe') `
                    -Source "registry:$registryPath/$propertyName"
            }
            else {
                Add-UniqueRhinoCandidate `
                    -Candidates $candidates `
                    -Path (Join-Path $trimmedPath 'System\Rhino.exe') `
                    -Source "registry:$registryPath/$propertyName"
                Add-UniqueRhinoCandidate `
                    -Candidates $candidates `
                    -Path (Join-Path $trimmedPath 'Rhino.exe') `
                    -Source "registry:$registryPath/$propertyName"
            }
        }
    }

    return $candidates.ToArray()
}

function Resolve-RhinoInstallation {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Definition
    )

    $valid = New-Object 'System.Collections.Generic.List[object]'
    foreach ($candidate in @(Get-RhinoCandidates -Definition $Definition)) {
        $rhinoPath = [string] $candidate.path
        if (-not (Test-Path -LiteralPath $rhinoPath -PathType Leaf)) {
            continue
        }

        $rhinoItem = Get-Item -LiteralPath $rhinoPath -Force
        if (($rhinoItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            Write-Warning "$($Definition.label) candidate is a reparse point and was ignored: '$rhinoPath'."
            continue
        }

        $rawVersion = [string] $rhinoItem.VersionInfo.FileVersion
        $versionMatch = [regex]::Match($rawVersion, '\d+(?:\.\d+){1,3}')
        if (-not $versionMatch.Success) {
            Write-Warning "$($Definition.label) candidate version could not be parsed: '$rhinoPath'."
            continue
        }

        $actualVersion = [version] $versionMatch.Value
        if ($actualVersion.Major -ne [int] $Definition.majorVersion) {
            Write-Warning (
                "$($Definition.label) candidate reports version $actualVersion and was ignored: " +
                "'$rhinoPath'.")
            continue
        }

        $yakPath = Join-Path (Split-Path -Parent $rhinoPath) 'yak.exe'
        if (-not (Test-Path -LiteralPath $yakPath -PathType Leaf)) {
            Write-Warning "$($Definition.label) has no sibling yak.exe: '$rhinoPath'."
            continue
        }
        $yakItem = Get-Item -LiteralPath $yakPath -Force
        if (($yakItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            Write-Warning "$($Definition.label) yak.exe is a reparse point and was ignored: '$yakPath'."
            continue
        }

        $valid.Add([pscustomobject] [ordered] @{
            name = [string] $Definition.name
            label = [string] $Definition.label
            target = [string] $Definition.id
            majorVersion = [int] $Definition.majorVersion
            version = $actualVersion.ToString()
            rhino = $rhinoPath
            yak = [System.IO.Path]::GetFullPath($yakPath)
            source = [string] $candidate.source
        })
    }

    if ($valid.Count -eq 0) {
        return $null
    }
    if ($valid.Count -gt 1) {
        Write-Warning (
            "Multiple $($Definition.label) installations were found; using '$($valid[0].rhino)'.")
    }

    return $valid[0]
}

function Find-StrayManualDragonGhas {
    $roots = New-Object 'System.Collections.Generic.List[string]'
    if (-not [string]::IsNullOrWhiteSpace($env:APPDATA)) {
        $roots.Add((Join-Path $env:APPDATA 'Grasshopper\Libraries'))
        $roots.Add((Join-Path $env:APPDATA 'McNeel\Rhinoceros\7.0\Plug-ins\Grasshopper\Libraries'))
        $roots.Add((Join-Path $env:APPDATA 'McNeel\Rhinoceros\8.0\Plug-ins\Grasshopper\Libraries'))
    }
    if (-not [string]::IsNullOrWhiteSpace($env:PROGRAMDATA)) {
        $roots.Add((Join-Path $env:PROGRAMDATA 'Grasshopper\Libraries'))
    }

    $expectedNames = @(
        'GonieGonie.InvisibleDragon.GH.gha',
        'GonieGonie.SimpleDragon.GH.gha'
    )
    $matches = New-Object 'System.Collections.Generic.List[string]'
    foreach ($rootPath in $roots) {
        if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) {
            continue
        }

        $files = @(Get-ChildItem `
            -LiteralPath $rootPath `
            -File `
            -Recurse `
            -ErrorAction SilentlyContinue |
            Where-Object { $expectedNames -contains $_.Name })
        foreach ($file in $files) {
            $fullPath = [System.IO.Path]::GetFullPath($file.FullName)
            if (@($matches | Where-Object {
                $_.Equals($fullPath, [System.StringComparison]::OrdinalIgnoreCase)
            }).Count -eq 0) {
                $matches.Add($fullPath)
            }
        }
    }

    return $matches.ToArray()
}

function Invoke-YakCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Executable,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [AllowNull()]
        [AllowEmptyString()]
        [string] $LogPath,

        [Parameter(Mandatory = $true)]
        [string] $FailureMessage
    )

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
    foreach ($line in $lines) {
        Write-Host $line
    }

    if (-not [string]::IsNullOrWhiteSpace($LogPath)) {
        [System.IO.File]::WriteAllText(
            $LogPath,
            ($lines -join [Environment]::NewLine) + [Environment]::NewLine,
            [System.Text.UTF8Encoding]::new($false))
    }

    if ($exitCode -ne 0) {
        $logSuffix = if ([string]::IsNullOrWhiteSpace($LogPath)) {
            ''
        }
        else {
            " See '$LogPath'."
        }
        throw "$FailureMessage (exit code $exitCode).$logSuffix"
    }

    return @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Test-YakPackageListed {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Lines,

        [Parameter(Mandatory = $true)]
        [string] $ProductId,

        [AllowNull()]
        [AllowEmptyString()]
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
    return @($Lines | Where-Object {
        [regex]::IsMatch(
            $_,
            $pattern,
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    }).Count -eq 1
}

function Get-SafeLogName {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    return ($Value.ToLowerInvariant() -replace '[^a-z0-9-]', '-')
}

$rawArguments = @(@(
    [string] $env:DRAGONS_INSTALLER_ARG1,
    [string] $env:DRAGONS_INSTALLER_ARG2,
    [string] $env:DRAGONS_INSTALLER_ARG3
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

$selectedTarget = 'all'
$targetWasSpecified = $false
$checkOnly = $false
$helpRequested = $false
foreach ($argument in $rawArguments) {
    $normalized = $argument.Trim().ToLowerInvariant()
    switch ($normalized) {
        '--check' {
            if ($checkOnly) {
                throw "Argument '--check' was supplied more than once."
            }
            $checkOnly = $true
        }
        '--help' { $helpRequested = $true }
        '-h' { $helpRequested = $true }
        '/?' { $helpRequested = $true }
        'all' {
            if ($targetWasSpecified) {
                throw 'More than one Rhino target was supplied.'
            }
            $selectedTarget = 'all'
            $targetWasSpecified = $true
        }
        'rhino7' {
            if ($targetWasSpecified) {
                throw 'More than one Rhino target was supplied.'
            }
            $selectedTarget = 'rhino7'
            $targetWasSpecified = $true
        }
        'rhino8' {
            if ($targetWasSpecified) {
                throw 'More than one Rhino target was supplied.'
            }
            $selectedTarget = 'rhino8'
            $targetWasSpecified = $true
        }
        default {
            throw "Unknown installer argument '$argument'. Run with --help for usage."
        }
    }
}

if ($helpRequested) {
    if ($rawArguments.Count -ne 1) {
        throw '--help cannot be combined with another installer argument.'
    }
    Show-InstallerUsage
    exit 0
}
if ($rawArguments.Count -gt 2) {
    throw 'At most one target and --check may be supplied.'
}

$bundleRoot = [System.IO.Path]::GetFullPath(
    [string] $env:DRAGONS_INSTALLER_ROOT).TrimEnd('\', '/')
$installerPath = [System.IO.Path]::GetFullPath(
    [string] $env:DRAGONS_INSTALLER_FILE)
if (-not (Test-PathWithin -Root $bundleRoot -Candidate $installerPath)) {
    throw "Installer is outside its extracted bundle root: '$installerPath'."
}
Assert-NoBundleReparsePoint -BundleRoot $bundleRoot -Candidate $installerPath

Write-Host "Dragons for Grasshopper $ExpectedVersion"
Write-Host "Bundle: $bundleRoot"
Write-Host "Mode: $(if ($checkOnly) { 'check only' } else { 'replace and install' })"
Write-Host "Target: $selectedTarget"
Write-Host ''

Write-Host 'Verifying the complete four-package bundle before any changes...'
$release = Read-AndVerifyReleaseManifest -BundleRoot $bundleRoot
foreach ($package in @($release.packages)) {
    Write-Host (
        "Verified $($package.displayName) $($package.target): " +
        "$($package.relativePath) [$($package.sha256)]")
}
Verify-BundleChecksumInventory -BundleRoot $bundleRoot -Release $release
Write-Host 'Verified the exact nine-file internal checksum inventory.'

$strayGhas = @(Find-StrayManualDragonGhas)
if ($strayGhas.Count -ne 0) {
    Write-Warning (
        'Manual Dragon GHA files were found outside Rhino Package Manager. ' +
        'They will not be deleted automatically and may cause duplicate loading:')
    foreach ($strayPath in $strayGhas) {
        Write-Warning "  $strayPath"
    }
}

$requestedDefinitions = @($TargetDefinitions | Where-Object {
    $selectedTarget -eq 'all' -or [string] $_.id -eq $selectedTarget
})
$resolvedRhinoInstallations = New-Object 'System.Collections.Generic.List[object]'
foreach ($definition in $requestedDefinitions) {
    $resolvedRhino = Resolve-RhinoInstallation -Definition $definition
    if ($null -eq $resolvedRhino) {
        if ($selectedTarget -ne 'all') {
            throw "$($definition.label) and its sibling yak.exe were not found."
        }
        Write-Warning "$($definition.label) is unavailable and will be skipped."
        continue
    }

    $resolvedRhinoInstallations.Add($resolvedRhino)
    Write-Host (
        "$($resolvedRhino.label): $($resolvedRhino.version) at " +
        "'$($resolvedRhino.rhino)' [$($resolvedRhino.source)]")
}
if ($resolvedRhinoInstallations.Count -eq 0) {
    throw 'No supported Rhino 7 or Rhino 8 installation with yak.exe was found.'
}

$runningRhino = @(Get-Process -Name 'Rhino' -ErrorAction SilentlyContinue)
if ($runningRhino.Count -ne 0) {
    $processIds = @($runningRhino | ForEach-Object { [string] $_.Id }) -join ', '
    throw (
        'Close every Rhino process before checking or replacing Dragon packages. ' +
        "Running process IDs: $processIds.")
}

if ($checkOnly) {
    foreach ($resolvedRhino in $resolvedRhinoInstallations) {
        Write-Host ''
        Write-Host "Checking $($resolvedRhino.label) Package Manager..."
        $null = @(Invoke-YakCommand `
            -Executable ([string] $resolvedRhino.yak) `
            -Arguments @('list') `
            -LogPath $null `
            -FailureMessage "$($resolvedRhino.label) package listing failed")
    }

    Write-Host ''
    Write-Host (
        "Check passed: all four $ExpectedVersion packages and " +
        "$($resolvedRhinoInstallations.Count) selected Rhino installation(s) are ready.")
    exit 0
}

# Unblocking known, already verified package files changes only the NTFS zone
# alternate stream, not their bytes. It occurs after the complete preflight and
# before any installed package is removed.
foreach ($package in @($release.packages)) {
    Unblock-File -LiteralPath ([string] $package.path)
}

$logRoot = Join-Path `
    ([System.IO.Path]::GetTempPath()) `
    (Join-Path 'GonieGonie\Dragons' (
        'install-{0}-{1}' -f `
            [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'),
            $PID))
$null = New-Item -ItemType Directory -Path $logRoot -Force

$installations = New-Object 'System.Collections.Generic.List[object]'
foreach ($resolvedRhino in $resolvedRhinoInstallations) {
    $hostLogName = Get-SafeLogName -Value ([string] $resolvedRhino.name)
    $hostPackages = @($release.packages | Where-Object {
        [string] $_.target -eq [string] $resolvedRhino.target
    })
    if ($hostPackages.Count -ne $ProductDefinitions.Count) {
        throw "$($resolvedRhino.label) did not resolve exactly two verified product packages."
    }

    Write-Host ''
    Write-Host "Replacing Dragon packages in $($resolvedRhino.label)..."
    $installedBefore = @(Invoke-YakCommand `
        -Executable ([string] $resolvedRhino.yak) `
        -Arguments @('list') `
        -LogPath (Join-Path $logRoot "$hostLogName-list-before.log") `
        -FailureMessage "$($resolvedRhino.label) package listing failed")

    foreach ($product in $ProductDefinitions) {
        $productId = [string] $product.id
        if (Test-YakPackageListed `
                -Lines $installedBefore `
                -ProductId $productId `
                -Version $null) {
            $null = @(Invoke-YakCommand `
                -Executable ([string] $resolvedRhino.yak) `
                -Arguments @('uninstall', $productId) `
                -LogPath (Join-Path $logRoot "$hostLogName-uninstall-$productId.log") `
                -FailureMessage "$($resolvedRhino.label) could not uninstall $productId")
        }
        else {
            Write-Host "$productId is not installed in $($resolvedRhino.label); continuing."
        }
    }

    $installedAfterRemoval = @(Invoke-YakCommand `
        -Executable ([string] $resolvedRhino.yak) `
        -Arguments @('list') `
        -LogPath (Join-Path $logRoot "$hostLogName-list-after-removal.log") `
        -FailureMessage "$($resolvedRhino.label) post-removal package listing failed")
    foreach ($product in $ProductDefinitions) {
        if (Test-YakPackageListed `
                -Lines $installedAfterRemoval `
                -ProductId ([string] $product.id) `
                -Version $null) {
            throw "$($product.id) remains installed in $($resolvedRhino.label) after uninstall."
        }
    }

    foreach ($package in $hostPackages) {
        $productId = [string] $package.product
        $null = @(Invoke-YakCommand `
            -Executable ([string] $resolvedRhino.yak) `
            -Arguments @('install', [string] $package.path) `
            -LogPath (Join-Path $logRoot "$hostLogName-install-$productId.log") `
            -FailureMessage "$($resolvedRhino.label) could not install $productId")
    }

    $installedFinal = @(Invoke-YakCommand `
        -Executable ([string] $resolvedRhino.yak) `
        -Arguments @('list') `
        -LogPath (Join-Path $logRoot "$hostLogName-list-final.log") `
        -FailureMessage "$($resolvedRhino.label) final package listing failed")
    foreach ($product in $ProductDefinitions) {
        if (-not (Test-YakPackageListed `
                -Lines $installedFinal `
                -ProductId ([string] $product.id) `
                -Version $ExpectedVersion)) {
            throw (
                "$($product.id) $ExpectedVersion is not listed after installation " +
                "in $($resolvedRhino.label).")
        }
    }

    $installations.Add([pscustomobject] [ordered] @{
        target = [string] $resolvedRhino.target
        rhinoVersion = [string] $resolvedRhino.version
        rhinoExecutable = [string] $resolvedRhino.rhino
        yakExecutable = [string] $resolvedRhino.yak
        products = @($ProductDefinitions | ForEach-Object { [string] $_.id })
    })
}

$result = [pscustomobject] [ordered] @{
    schema = 'goniegonie.dragons-grasshopper.windows-install-result.v1'
    status = 'installed'
    version = $ExpectedVersion
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    selectedTarget = $selectedTarget
    bundleRoot = $bundleRoot
    installations = $installations.ToArray()
}
$resultPath = Join-Path $logRoot 'install-result.json'
[System.IO.File]::WriteAllText(
    $resultPath,
    ($result | ConvertTo-Json -Depth 8) + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))

Write-Host ''
Write-Host (
    "Dragons $ExpectedVersion installation complete for: " +
    (@($resolvedRhinoInstallations | ForEach-Object { $_.label }) -join ', ') + '.')
Write-Host 'Start Rhino, open Grasshopper, and confirm the InvisibleDragon and SimpleDragon tabs.'
Write-Host "Installer logs: $logRoot"

# Shared helpers for the repository's Windows bootstrap and build scripts.
# Keep this file compatible with Windows PowerShell 5.1.

Set-StrictMode -Version 2.0

function Get-RepositoryRoot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $ScriptDirectory
    )

    $root = [System.IO.Path]::GetFullPath((Join-Path $ScriptDirectory '..'))
    $globalJson = Join-Path $root 'global.json'
    $nugetConfig = Join-Path $root 'NuGet.config'

    if (-not (Test-Path -LiteralPath $globalJson -PathType Leaf)) {
        throw "Repository safety check failed: global.json was not found under '$root'."
    }

    if (-not (Test-Path -LiteralPath $nugetConfig -PathType Leaf)) {
        throw "Repository safety check failed: NuGet.config was not found under '$root'."
    }

    return $root.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}

function Assert-RepositoryChildPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string[]] $AllowedTopLevelNames
    )

    $root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    $candidate = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $prefix = $root + [System.IO.Path]::DirectorySeparatorChar

    if (-not $candidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside the repository: '$candidate'."
    }

    $relative = $candidate.Substring($prefix.Length)
    if ([string]::IsNullOrWhiteSpace($relative)) {
        throw 'Refusing to operate on the repository root.'
    }

    $topLevel = $relative.Split(@('\', '/'), [System.StringSplitOptions]::RemoveEmptyEntries)[0]
    $allowed = $false
    foreach ($name in $AllowedTopLevelNames) {
        if ($topLevel.Equals($name, [System.StringComparison]::OrdinalIgnoreCase)) {
            $allowed = $true
            break
        }
    }

    if (-not $allowed) {
        throw "Refusing to operate on '$candidate'; allowed top-level directories: $($AllowedTopLevelNames -join ', ')."
    }

    return $candidate
}

function Assert-NoReparsePoints {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [string] $AnchorPath
    )

    if (-not [string]::IsNullOrWhiteSpace($AnchorPath)) {
        $anchor = [System.IO.Path]::GetFullPath($AnchorPath).TrimEnd('\', '/')
        $candidate = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
        $prefix = $anchor + [System.IO.Path]::DirectorySeparatorChar
        if (-not $candidate.Equals($anchor, [System.StringComparison]::OrdinalIgnoreCase) -and
            -not $candidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to inspect reparse points outside anchor '$anchor': '$candidate'."
        }
        if (-not (Test-Path -LiteralPath $anchor -PathType Container)) {
            throw "Reparse-point safety anchor does not exist: '$anchor'."
        }

        $current = $anchor
        $relative = if ($candidate.Equals(
            $anchor,
            [System.StringComparison]::OrdinalIgnoreCase)) {
            ''
        }
        else {
            $candidate.Substring($prefix.Length)
        }
        $segments = @($relative.Split(
            @('\', '/'),
            [System.StringSplitOptions]::RemoveEmptyEntries))
        $anchorItem = Get-Item -LiteralPath $current -Force
        if (($anchorItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to operate through reparse point '$current'."
        }
        foreach ($segment in $segments) {
            $current = Join-Path $current $segment
            if (-not (Test-Path -LiteralPath $current)) {
                break
            }
            $ancestorItem = Get-Item -LiteralPath $current -Force
            if (($ancestorItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing to operate through reparse point '$current'."
            }
        }
    }

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $rootItem = Get-Item -LiteralPath $Path -Force
    if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to recursively remove reparse point '$Path'."
    }

    $reparsePoint = Get-ChildItem -LiteralPath $Path -Force -Recurse -ErrorAction Stop |
        Where-Object { ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 } |
        Select-Object -First 1

    if ($null -ne $reparsePoint) {
        throw "Refusing to recursively remove '$Path' because it contains reparse point '$($reparsePoint.FullName)'."
    }
}

function Ensure-Directory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (Test-Path -LiteralPath $Path -PathType Container) {
        return
    }

    if ($WhatIfPreference) {
        Write-Host "What if: create directory '$Path'."
        return
    }

    $null = New-Item -ItemType Directory -Path $Path -Force
}

function Get-Sha256 {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $stream = $null
    $sha256 = $null
    try {
        # Use the framework API because Get-FileHash's provider lookup inherits
        # a caller's -WhatIf preference on Windows PowerShell 5.1.
        $stream = [System.IO.File]::OpenRead($Path)
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        $bytes = $sha256.ComputeHash($stream)
    }
    finally {
        if ($null -ne $sha256) {
            $sha256.Dispose()
        }
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
    return ([System.BitConverter]::ToString($bytes) -replace '-', '').ToLowerInvariant()
}

function Get-Sha512 {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $stream = $null
    $sha512 = $null
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        $sha512 = [System.Security.Cryptography.SHA512]::Create()
        $bytes = $sha512.ComputeHash($stream)
    }
    finally {
        if ($null -ne $sha512) {
            $sha512.Dispose()
        }
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
    return ([System.BitConverter]::ToString($bytes) -replace '-', '').ToLowerInvariant()
}

function Write-Utf8JsonIfChanged {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object] $InputObject,

        [Parameter(Mandatory = $true)]
        [string] $Path,

        [int] $Depth = 8
    )

    $json = ($InputObject | ConvertTo-Json -Depth $Depth) + [Environment]::NewLine
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $existing = [System.IO.File]::ReadAllText($Path)
        if ($existing -eq $json) {
            Write-Host "Unchanged: $Path"
            return
        }
    }

    if ($WhatIfPreference) {
        Write-Host "What if: write generated configuration '$Path'."
        return
    }

    Ensure-Directory -Path (Split-Path -Parent $Path)
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $json, $utf8WithoutBom)
    Write-Host "Wrote: $Path"
}

function Format-NativeCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [string[]] $ArgumentList = @()
    )

    $formattedArguments = foreach ($argument in $ArgumentList) {
        if ($argument -match '[\s"]') {
            '"' + ($argument -replace '"', '\"') + '"'
        }
        else {
            $argument
        }
    }

    return (@($FilePath) + @($formattedArguments)) -join ' '
}

function Invoke-LoggedNativeCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [string[]] $ArgumentList = @(),

        [Parameter(Mandatory = $true)]
        [string] $LogPath,

        [Parameter(Mandatory = $true)]
        [string] $FailureMessage
    )

    Ensure-Directory -Path (Split-Path -Parent $LogPath)
    Write-Host ("> " + (Format-NativeCommand -FilePath $FilePath -ArgumentList $ArgumentList))

    if ($WhatIfPreference) {
        Write-Host "What if: run the command and write '$LogPath'."
        return
    }

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell turns redirected native stderr into non-terminating
        # error records. Let the process exit code, not stream classification,
        # decide whether the command failed.
        $ErrorActionPreference = 'Continue'
        & $FilePath @ArgumentList 2>&1 |
            Tee-Object -FilePath $LogPath |
            ForEach-Object { Write-Host $_ }
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "$FailureMessage (exit code $exitCode). See '$LogPath'."
    }
}

function Set-RepositoryBuildEnvironment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string] $DotNetExecutable
    )

    $tools = Join-Path $RepositoryRoot '.tools'
    $temp = Join-Path $RepositoryRoot 'temp'

    $env:DOTNET_ROOT = Split-Path -Parent $DotNetExecutable
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $env:DOTNET_CLI_HOME = Join-Path $tools 'dotnet-cli-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    $env:NUGET_PACKAGES = Join-Path $tools 'nuget\packages'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $tools 'nuget\http-cache'
    $env:NUGET_SCRATCH = Join-Path $temp 'nuget\scratch'

    Ensure-Directory -Path $env:DOTNET_CLI_HOME
    Ensure-Directory -Path $env:NUGET_PACKAGES
    Ensure-Directory -Path $env:NUGET_HTTP_CACHE_PATH
    Ensure-Directory -Path $env:NUGET_SCRATCH
}

function Find-SolutionFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    $preferred = Join-Path $RepositoryRoot 'Dragons.Grasshopper.sln'
    if (Test-Path -LiteralPath $preferred -PathType Leaf) {
        return $preferred
    }

    $solutions = @(Get-ChildItem -LiteralPath $RepositoryRoot -Filter '*.sln' -File -ErrorAction SilentlyContinue)
    if ($solutions.Count -eq 1) {
        return $solutions[0].FullName
    }

    if ($solutions.Count -gt 1) {
        throw "Multiple solution files were found. Expected '$preferred'."
    }

    return $null
}

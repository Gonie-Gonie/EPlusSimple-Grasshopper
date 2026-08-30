#requires -Version 5.1

function Get-OptionalObjectProperty {
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

function ConvertTo-RhinoExecutablePath {
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string] $Path,

        [ValidateSet('Executable', 'RootOrExecutable')]
        [string] $PathKind = 'RootOrExecutable'
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    try {
        $fullPath = [System.IO.Path]::GetFullPath($Path.Trim())
    }
    catch {
        throw "Rhino path is invalid: '$Path'."
    }

    $trimmedPath = $fullPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $leafName = [System.IO.Path]::GetFileName($trimmedPath)
    if ($leafName.Equals('Rhino.exe', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $trimmedPath
    }

    if ($PathKind -eq 'Executable') {
        throw "Configured Rhino executable must be named Rhino.exe: '$fullPath'."
    }
    if ([System.IO.Path]::GetExtension($leafName).Equals(
            '.exe',
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Rhino path names an unexpected executable instead of Rhino.exe: '$fullPath'."
    }

    if ($leafName.Equals('System', [System.StringComparison]::OrdinalIgnoreCase)) {
        return Join-Path $trimmedPath 'Rhino.exe'
    }

    return Join-Path $trimmedPath 'System\Rhino.exe'
}

function Read-RhinoLocalSettings {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    try {
        $settings = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "Local settings could not be read as JSON: '$Path'. $($_.Exception.Message)"
    }

    $schema = [string] (Get-OptionalObjectProperty -InputObject $settings -Name 'schema')
    if ($schema -ne 'goniegonie.dragons-grasshopper.local-settings.v1') {
        throw "Unsupported local settings schema '$schema' in '$Path'. Run 'dev.cmd setup' again."
    }

    return $settings
}

function Resolve-RhinoHostCandidate {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Rhino7', 'Rhino8')]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [ValidateRange(7, 8)]
        [int] $MajorVersion,

        [Parameter(Mandatory = $true)]
        [ValidateSet('rhino7', 'rhino8')]
        [string] $PackageTarget,

        [AllowNull()]
        [AllowEmptyString()]
        [string] $ExplicitPath,

        [AllowNull()]
        [object] $LocalSettings,

        [Parameter(Mandatory = $true)]
        [string] $StandardExecutable
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        return [pscustomobject] [ordered] @{
            name = $Name
            majorVersion = $MajorVersion
            packageTarget = $PackageTarget
            source = 'install-argument'
            rhino = ConvertTo-RhinoExecutablePath -Path $ExplicitPath -PathKind RootOrExecutable
        }
    }

    if ($null -ne $LocalSettings) {
        $schema = [string] (Get-OptionalObjectProperty -InputObject $LocalSettings -Name 'schema')
        if ($schema -ne 'goniegonie.dragons-grasshopper.local-settings.v1') {
            throw "Unsupported local settings schema '$schema'. Run 'dev.cmd setup' again."
        }

        $rhinoSettings = Get-OptionalObjectProperty -InputObject $LocalSettings -Name 'rhino'
        $settingsKey = $Name.ToLowerInvariant()
        $hostSettings = Get-OptionalObjectProperty -InputObject $rhinoSettings -Name $settingsKey
        $configuredExecutable = [string] (
            Get-OptionalObjectProperty -InputObject $hostSettings -Name 'executable')
        $configuredRoot = [string] (
            Get-OptionalObjectProperty -InputObject $hostSettings -Name 'root')
        $hasConfiguredPath =
            -not [string]::IsNullOrWhiteSpace($configuredExecutable) -or
            -not [string]::IsNullOrWhiteSpace($configuredRoot)

        if ($hasConfiguredPath) {
            $status = [string] (Get-OptionalObjectProperty -InputObject $hostSettings -Name 'status')
            if ($status -ne 'ready') {
                throw "$Name has a configured path but local settings status is '$status', not 'ready'."
            }

            $fromExecutable = $null
            if (-not [string]::IsNullOrWhiteSpace($configuredExecutable)) {
                $fromExecutable = ConvertTo-RhinoExecutablePath `
                    -Path $configuredExecutable `
                    -PathKind Executable
            }

            $fromRoot = $null
            if (-not [string]::IsNullOrWhiteSpace($configuredRoot)) {
                $fromRoot = ConvertTo-RhinoExecutablePath `
                    -Path $configuredRoot `
                    -PathKind RootOrExecutable
            }

            if ($null -ne $fromExecutable -and
                $null -ne $fromRoot -and
                -not $fromExecutable.Equals(
                    $fromRoot,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "$Name local settings executable and root resolve to different installations."
            }

            $selectedPath = if ($null -ne $fromExecutable) {
                $fromExecutable
            }
            else {
                $fromRoot
            }
            return [pscustomobject] [ordered] @{
                name = $Name
                majorVersion = $MajorVersion
                packageTarget = $PackageTarget
                source = 'local-settings'
                rhino = $selectedPath
            }
        }
    }

    return [pscustomobject] [ordered] @{
        name = $Name
        majorVersion = $MajorVersion
        packageTarget = $PackageTarget
        source = 'standard-location'
        rhino = ConvertTo-RhinoExecutablePath -Path $StandardExecutable -PathKind Executable
    }
}

function Get-RhinoExecutablePair {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $Executable
    )

    $resolvedExecutable = ConvertTo-RhinoExecutablePath `
        -Path $Executable `
        -PathKind Executable
    if (-not (Test-Path -LiteralPath $resolvedExecutable -PathType Leaf)) {
        throw "$Name Rhino.exe is missing: '$resolvedExecutable'."
    }

    $yakExecutable = Join-Path (Split-Path -Parent $resolvedExecutable) 'yak.exe'
    if (-not (Test-Path -LiteralPath $yakExecutable -PathType Leaf)) {
        throw "$Name sibling yak.exe is missing next to Rhino.exe: '$yakExecutable'."
    }

    return [pscustomobject] [ordered] @{
        rhino = $resolvedExecutable
        yak = [System.IO.Path]::GetFullPath($yakExecutable)
    }
}

function Confirm-RhinoHostCandidate {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Candidate
    )

    $name = [string] (Get-OptionalObjectProperty -InputObject $Candidate -Name 'name')
    $expectedMajor = [int] (Get-OptionalObjectProperty -InputObject $Candidate -Name 'majorVersion')
    $pair = Get-RhinoExecutablePair `
        -Name $name `
        -Executable ([string] (Get-OptionalObjectProperty -InputObject $Candidate -Name 'rhino'))

    $item = Get-Item -LiteralPath $pair.rhino
    $rawVersion = [string] $item.VersionInfo.FileVersion
    $versionMatch = [regex]::Match($rawVersion, '\d+(?:\.\d+){1,3}')
    if (-not $versionMatch.Success) {
        throw "$name Rhino.exe version could not be parsed from '$rawVersion'."
    }

    $actualVersion = [version] $versionMatch.Value
    if ($actualVersion.Major -ne $expectedMajor) {
        throw "$name requires Rhino major version $expectedMajor, but '$($pair.rhino)' reports $actualVersion."
    }

    return [pscustomobject] [ordered] @{
        name = $name
        majorVersion = $expectedMajor
        version = $actualVersion.ToString()
        packageTarget = [string] (
            Get-OptionalObjectProperty -InputObject $Candidate -Name 'packageTarget')
        source = [string] (Get-OptionalObjectProperty -InputObject $Candidate -Name 'source')
        rhino = [string] $pair.rhino
        yak = [string] $pair.yak
    }
}

function Get-RhinoSetupArguments {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]] $Hosts
    )

    $arguments = New-Object 'System.Collections.Generic.List[string]'
    foreach ($hostDefinition in @(
        [pscustomobject] @{ name = 'Rhino7'; pathParameter = '-Rhino7Path'; requireParameter = '-RequireRhino7' },
        [pscustomobject] @{ name = 'Rhino8'; pathParameter = '-Rhino8Path'; requireParameter = '-RequireRhino8' }
    )) {
        $matches = @($Hosts | Where-Object {
            [string] (Get-OptionalObjectProperty -InputObject $_ -Name 'name') -eq $hostDefinition.name
        })
        if ($matches.Count -gt 1) {
            throw "Resolved host list contains more than one $($hostDefinition.name) installation."
        }
        if ($matches.Count -eq 0) {
            continue
        }

        $executable = [string] (
            Get-OptionalObjectProperty -InputObject $matches[0] -Name 'rhino')
        if ([string]::IsNullOrWhiteSpace($executable)) {
            throw "$($hostDefinition.name) has no Rhino.exe path to forward to setup."
        }

        $arguments.Add([string] $hostDefinition.pathParameter)
        $arguments.Add($executable)
        $arguments.Add([string] $hostDefinition.requireParameter)
    }

    return [string[]] $arguments.ToArray()
}

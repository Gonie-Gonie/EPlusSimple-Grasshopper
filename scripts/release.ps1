#requires -Version 5.1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
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
$upstreamRoot = Join-Path $repositoryRoot 'temp\reference\upstream\eplussimple'
$upstreamGatePath = Join-Path $repositoryRoot 'temp\upstream-tracker\compatibility-gate.json'
$upstreamReleasePath = Join-Path $releaseRoot 'upstream-compatibility-gate.json'
$trustedEvidenceReleaseRoot = Join-Path $releaseRoot 'trusted-evidence'

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

function Assert-EngineeringPortProvenance {
    param(
        [Parameter(Mandatory = $true)] [object] $Report,
        [Parameter(Mandatory = $true)] [string] $ExpectedCommit
    )
    $provenance = $Report.port_provenance
    if ($null -eq $provenance -or
        [string] $provenance.schema -cne 'goniegonie.dragons.engineering-port-provenance.v1' -or
        [string] $provenance.git.commit -cne $ExpectedCommit -or
        $provenance.git.dirty -isnot [bool] -or [bool] $provenance.git.dirty) {
        throw 'Engineering compatibility is not bound to the clean release HEAD.'
    }

    $sourceRoots = @(
        'src/Shared/GonieGonie.BuildingEnergy.Contracts',
        'src/Shared/GonieGonie.EnergyPlus.Runtime',
        'src/InvisibleDragon/GonieGonie.InvisibleDragon.Core',
        'src/SimpleDragon/GonieGonie.SimpleDragon.Core',
        'tools/compatibility-runner'
    )
    $expectedPaths = @($sourceRoots | ForEach-Object {
        Get-ChildItem -LiteralPath (Join-Path $repositoryRoot $_) -File -Recurse |
            Where-Object { $_.Extension -in @('.cs', '.csproj') } |
            ForEach-Object { Get-RelativeUnixPath -Root $repositoryRoot -Path $_.FullName }
    } | Sort-Object -Unique)
    $reportedFiles = @($provenance.production_source_set.files)
    $reportedPaths = @($reportedFiles | ForEach-Object { [string] $_.path })
    if ([int] $provenance.production_source_set.file_count -ne $reportedFiles.Count -or
        $reportedFiles.Count -ne $expectedPaths.Count -or
        @(Compare-Object -CaseSensitive -ReferenceObject $expectedPaths -DifferenceObject @($reportedPaths | Sort-Object)).Count -ne 0 -or
        @($reportedPaths | Sort-Object -Unique).Count -ne $reportedPaths.Count) {
        throw 'Engineering production source-set membership differs from the release tree.'
    }
    $sourceLines = @()
    foreach ($entry in @($reportedFiles | Sort-Object path)) {
        $path = Join-Path $repositoryRoot ([string] $entry.path).Replace('/', '\')
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Engineering source is missing: '$path'." }
        $item = Get-Item -LiteralPath $path
        $hash = 'sha256:' + (Get-Sha256 -Path $path)
        if ([long] $entry.bytes -ne $item.Length -or [string] $entry.sha256 -cne $hash) {
            throw "Engineering source binding drifted: '$($entry.path)'."
        }
        $sourceLines += "$hash  $($item.Length)  $($entry.path)"
    }
    $sourceBytes = [System.Text.UTF8Encoding]::new($false).GetBytes(($sourceLines -join "`n") + "`n")
    $sourceHash = 'sha256:' + (Get-BytesSha256 -Bytes $sourceBytes)
    if ([string] $provenance.production_source_set.sha256 -cne $sourceHash) {
        throw 'Engineering production source-set aggregate hash drifted.'
    }

    $binaries = @($provenance.executed_binaries.files)
    $expectedAssemblies = @(
        'GonieGonie.BuildingEnergy.Contracts', 'GonieGonie.CompatibilityRunner',
        'GonieGonie.EnergyPlus.Runtime', 'GonieGonie.InvisibleDragon.Core',
        'GonieGonie.SimpleDragon.Core') | Sort-Object
    if ([string] $provenance.executed_binaries.target_framework -cne 'net8.0-windows' -or
        [string] $provenance.executed_binaries.configuration -cne 'Release' -or
        $provenance.executed_binaries.gha_executed -isnot [bool] -or
        [bool] $provenance.executed_binaries.gha_executed -or
        @(Compare-Object -CaseSensitive -ReferenceObject $expectedAssemblies -DifferenceObject @($binaries.assembly_name | Sort-Object)).Count -ne 0) {
        throw 'Engineering executed-binary identity is incomplete or falsely claims a GHA host.'
    }
    foreach ($entry in $binaries) {
        $path = Join-Path $repositoryRoot ([string] $entry.path).Replace('/', '\')
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Engineering binary is missing: '$path'." }
        $item = Get-Item -LiteralPath $path
        $identity = [Reflection.AssemblyName]::GetAssemblyName($path)
        if ([long] $entry.bytes -ne $item.Length -or
            [string] $entry.sha256 -cne ('sha256:' + (Get-Sha256 -Path $path)) -or
            [string] $entry.assembly_name -cne $identity.Name -or
            [string] $entry.assembly_version -cne $identity.Version.ToString() -or
            [string] $entry.target_framework -cne 'net8.0-windows') {
            throw "Engineering binary binding drifted: '$($entry.path)'."
        }
    }
}

function Get-BytesSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]] $Bytes
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash($Bytes)
        return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Assert-NoDuplicateJsonObjectKeys {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Text
    )

    function Skip-JsonWhitespace {
        param([string] $Json, [ref] $Position)
        while ($Position.Value -lt $Json.Length -and
            [char]::IsWhiteSpace($Json[$Position.Value])) {
            $Position.Value += 1
        }
    }

    function Read-JsonStringToken {
        param([string] $Json, [ref] $Position)
        $start = $Position.Value
        if ($start -ge $Json.Length -or $Json[$start] -ne '"') {
            throw 'Expected a JSON string token.'
        }
        $Position.Value += 1
        while ($Position.Value -lt $Json.Length) {
            $character = $Json[$Position.Value]
            if ($character -eq '"') {
                $Position.Value += 1
                return $Json.Substring($start, $Position.Value - $start)
            }
            if ([int] $character -lt 0x20) {
                throw 'JSON string contains an unescaped control character.'
            }
            if ($character -eq '\') {
                $Position.Value += 1
                if ($Position.Value -ge $Json.Length) {
                    throw 'JSON string ends inside an escape sequence.'
                }
                $escape = $Json[$Position.Value]
                $Position.Value += 1
                if ($escape -eq 'u') {
                    if ($Position.Value + 4 -gt $Json.Length) {
                        throw 'JSON string contains a truncated Unicode escape.'
                    }
                    $digits = $Json.Substring($Position.Value, 4)
                    if ($digits -cnotmatch '^[0-9a-fA-F]{4}$') {
                        throw 'JSON string contains an invalid Unicode escape.'
                    }
                    $Position.Value += 4
                }
                elseif ('"\/bfnrt'.IndexOf($escape) -lt 0) {
                    throw 'JSON string contains an invalid escape sequence.'
                }
                continue
            }
            $Position.Value += 1
        }
        throw 'JSON string is not terminated.'
    }

    function Read-JsonValueWithUniqueObjectKeys {
        param(
            [string] $Json,
            [ref] $Position,
            [int] $Depth
        )
        if ($Depth -gt 256) {
            throw 'JSON nesting exceeds the release safety limit.'
        }
        Skip-JsonWhitespace -Json $Json -Position $Position
        if ($Position.Value -ge $Json.Length) {
            throw 'JSON value is missing.'
        }
        $character = $Json[$Position.Value]
        if ($character -eq '{') {
            $Position.Value += 1
            $keys = [System.Collections.Generic.HashSet[string]]::new(
                [System.StringComparer]::OrdinalIgnoreCase)
            Skip-JsonWhitespace -Json $Json -Position $Position
            if ($Position.Value -lt $Json.Length -and $Json[$Position.Value] -eq '}') {
                $Position.Value += 1
                return
            }
            while ($true) {
                $token = Read-JsonStringToken -Json $Json -Position $Position
                try {
                    $key = ConvertFrom-Json -InputObject $token
                }
                catch {
                    throw "JSON object key is invalid: $($_.Exception.Message)"
                }
                if ($key -isnot [string] -or -not $keys.Add($key)) {
                    throw "JSON contains a duplicate or case-ambiguous object key '$key'."
                }
                Skip-JsonWhitespace -Json $Json -Position $Position
                if ($Position.Value -ge $Json.Length -or $Json[$Position.Value] -ne ':') {
                    throw 'JSON object key is missing its colon.'
                }
                $Position.Value += 1
                Read-JsonValueWithUniqueObjectKeys `
                    -Json $Json `
                    -Position $Position `
                    -Depth ($Depth + 1)
                Skip-JsonWhitespace -Json $Json -Position $Position
                if ($Position.Value -ge $Json.Length) {
                    throw 'JSON object is not terminated.'
                }
                if ($Json[$Position.Value] -eq '}') {
                    $Position.Value += 1
                    return
                }
                if ($Json[$Position.Value] -ne ',') {
                    throw 'JSON object members are not comma separated.'
                }
                $Position.Value += 1
                Skip-JsonWhitespace -Json $Json -Position $Position
            }
        }
        if ($character -eq '[') {
            $Position.Value += 1
            Skip-JsonWhitespace -Json $Json -Position $Position
            if ($Position.Value -lt $Json.Length -and $Json[$Position.Value] -eq ']') {
                $Position.Value += 1
                return
            }
            while ($true) {
                Read-JsonValueWithUniqueObjectKeys `
                    -Json $Json `
                    -Position $Position `
                    -Depth ($Depth + 1)
                Skip-JsonWhitespace -Json $Json -Position $Position
                if ($Position.Value -ge $Json.Length) {
                    throw 'JSON array is not terminated.'
                }
                if ($Json[$Position.Value] -eq ']') {
                    $Position.Value += 1
                    return
                }
                if ($Json[$Position.Value] -ne ',') {
                    throw 'JSON array values are not comma separated.'
                }
                $Position.Value += 1
            }
        }
        if ($character -eq '"') {
            $null = Read-JsonStringToken -Json $Json -Position $Position
            return
        }
        $start = $Position.Value
        while ($Position.Value -lt $Json.Length -and
            ',]}'.IndexOf($Json[$Position.Value]) -lt 0 -and
            -not [char]::IsWhiteSpace($Json[$Position.Value])) {
            $Position.Value += 1
        }
        if ($Position.Value -eq $start) {
            throw 'JSON contains an invalid value token.'
        }
        $token = $Json.Substring($start, $Position.Value - $start)
        if ($token -cnotmatch '^(?:true|false|null|-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)$') {
            throw "JSON contains a non-standard scalar token '$token'."
        }
    }

    $position = 0
    Read-JsonValueWithUniqueObjectKeys `
        -Json $Text `
        -Position ([ref] $position) `
        -Depth 0
    Skip-JsonWhitespace -Json $Text -Position ([ref] $position)
    if ($position -ne $Text.Length) {
        throw 'JSON contains trailing content.'
    }
}

function Read-JsonBytesOnce {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required release report is missing: '$Path'."
    }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
    try {
        $text = $strictUtf8.GetString($bytes)
        Assert-NoDuplicateJsonObjectKeys -Text $text
        $json = $text | ConvertFrom-Json
    }
    catch {
        throw "Release report '$Path' is not strict UTF-8 JSON: $($_.Exception.Message)"
    }
    return [pscustomobject] [ordered] @{
        bytes = [byte[]] $bytes
        json = $json
        sha256 = Get-BytesSha256 -Bytes $bytes
    }
}

function Write-BytesAtomicallyExclusive {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]] $Bytes
    )

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        throw "Atomic report parent is missing: '$parent'."
    }
    if (Test-Path -LiteralPath $Path) {
        throw "Refusing to overwrite release report '$Path'."
    }
    $temporary = Join-Path $parent (
        '.' + [System.IO.Path]::GetFileName($Path) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    $stream = $null
    try {
        $stream = [System.IO.FileStream]::new(
            $temporary,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None)
        $stream.Write($Bytes, 0, $Bytes.Length)
        $stream.Flush($true)
        $stream.Dispose()
        $stream = $null
        if (Test-Path -LiteralPath $Path) {
            throw "Release report destination appeared during atomic write: '$Path'."
        }
        [System.IO.File]::Move($temporary, $Path)
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
        if (Test-Path -LiteralPath $temporary -PathType Leaf) {
            [System.IO.File]::Delete($temporary)
        }
    }
}

function Assert-JsonTrue {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if ($Value -isnot [bool] -or $Value -ne $true) {
        throw "Release report property '$Label' must be the JSON boolean true."
    }
}

function Get-JsonInteger {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if ($Value -isnot [int] -and $Value -isnot [long]) {
        throw "Release report property '$Label' must be a JSON integer."
    }
    return [long] $Value
}

function Assert-JsonEmptyArray {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if ($Value -isnot [System.Array] -or @($Value).Count -ne 0) {
        throw "Release report property '$Label' must be an empty JSON array."
    }
}

function Get-TrustedEvidenceCanonicalBytesSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]] $Bytes
    )

    return 'sha256:' + (Get-BytesSha256 -Bytes $Bytes)
}

function Assert-TrustedEvidenceExactProperties {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string[]] $Expected,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if ($null -eq $Value -or $Value -isnot [pscustomobject]) {
        throw "Trusted evidence $Label must be a JSON object."
    }
    $actual = @($Value.PSObject.Properties.Name)
    if ($actual.Count -ne $Expected.Count) {
        throw "Trusted evidence $Label has an inexact property count."
    }
    $actualSet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($name in $actual) {
        if (-not $actualSet.Add([string] $name)) {
            throw "Trusted evidence $Label repeats property '$name'."
        }
    }
    foreach ($name in $Expected) {
        if (-not $actualSet.Contains($name)) {
            throw "Trusted evidence $Label is missing exact property '$name'."
        }
    }
}

function Assert-TrustedEvidenceCanonicalHash {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if ($Value -isnot [string] -or
        $Value -cnotmatch '^sha256:[0-9a-f]{64}$') {
        throw "Trusted evidence $Label must be a canonical sha256:<lowercase-hex> value."
    }
    return [string] $Value
}

function ConvertTo-TrustedEvidenceJsonString {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Value
    )

    $builder = [System.Text.StringBuilder]::new()
    $null = $builder.Append('"')
    for ($index = 0; $index -lt $Value.Length; $index += 1) {
        $character = $Value[$index]
        $code = [int] $character
        if ($code -eq 0x22) {
            $null = $builder.Append('\"')
        }
        elseif ($code -eq 0x5c) {
            $null = $builder.Append('\\')
        }
        elseif ($code -eq 0x08) {
            $null = $builder.Append('\b')
        }
        elseif ($code -eq 0x0c) {
            $null = $builder.Append('\f')
        }
        elseif ($code -eq 0x0a) {
            $null = $builder.Append('\n')
        }
        elseif ($code -eq 0x0d) {
            $null = $builder.Append('\r')
        }
        elseif ($code -eq 0x09) {
            $null = $builder.Append('\t')
        }
        elseif ($code -lt 0x20) {
            $null = $builder.Append('\u')
            $null = $builder.Append($code.ToString('x4'))
        }
        elseif ([char]::IsHighSurrogate($character)) {
            if ($index + 1 -ge $Value.Length -or
                -not [char]::IsLowSurrogate($Value[$index + 1])) {
                throw 'Trusted evidence canonical JSON contains an unpaired high surrogate.'
            }
            $null = $builder.Append($character)
            $index += 1
            $null = $builder.Append($Value[$index])
        }
        elseif ([char]::IsLowSurrogate($character)) {
            throw 'Trusted evidence canonical JSON contains an unpaired low surrogate.'
        }
        else {
            $null = $builder.Append($character)
        }
    }
    $null = $builder.Append('"')
    return $builder.ToString()
}

function ConvertTo-TrustedEvidenceCanonicalJson {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object] $Value,

        [int] $Depth = 0
    )

    if ($Depth -gt 64) {
        throw 'Trusted evidence canonical JSON exceeds the depth limit.'
    }
    if ($null -eq $Value) {
        return 'null'
    }
    if ($Value -is [string]) {
        return ConvertTo-TrustedEvidenceJsonString -Value ([string] $Value)
    }
    if ($Value -is [bool]) {
        if ([bool] $Value) {
            return 'true'
        }
        return 'false'
    }
    if ($Value -is [int] -or $Value -is [long]) {
        return ([long] $Value).ToString(
            [System.Globalization.CultureInfo]::InvariantCulture)
    }
    if ($Value -is [System.Array]) {
        $items = [System.Collections.Generic.List[string]]::new()
        foreach ($item in $Value) {
            $items.Add((ConvertTo-TrustedEvidenceCanonicalJson `
                -Value $item `
                -Depth ($Depth + 1)))
        }
        return '[' + ([string]::Join(',', $items)) + ']'
    }
    if ($Value -is [pscustomobject]) {
        $names = [System.Collections.Generic.List[string]]::new()
        foreach ($property in $Value.PSObject.Properties) {
            $names.Add([string] $property.Name)
        }
        $names.Sort([System.StringComparer]::Ordinal)
        $members = [System.Collections.Generic.List[string]]::new()
        foreach ($name in $names) {
            $propertyValue = $Value.PSObject.Properties[$name].Value
            $members.Add(
                (ConvertTo-TrustedEvidenceJsonString -Value $name) + ':' +
                (ConvertTo-TrustedEvidenceCanonicalJson `
                    -Value $propertyValue `
                    -Depth ($Depth + 1)))
        }
        return '{' + ([string]::Join(',', $members)) + '}'
    }
    throw "Trusted evidence canonical JSON contains unsupported type '$($Value.GetType().FullName)'."
}

function Get-TrustedEvidenceCanonicalDataSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object] $Value
    )

    $json = ConvertTo-TrustedEvidenceCanonicalJson -Value $Value
    $encoding = [System.Text.UTF8Encoding]::new($false, $true)
    return Get-TrustedEvidenceCanonicalBytesSha256 -Bytes $encoding.GetBytes($json)
}

function Get-TrustedEvidenceCanonicalRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if ($Value -isnot [string] -or
        [string]::IsNullOrWhiteSpace($Value) -or
        $Value -cne $Value.Trim() -or
        $Value.Contains('\') -or
        $Value.StartsWith('/') -or
        $Value.Contains(':') -or
        $Value.IndexOfAny([char[]] '<>"|?*') -ge 0) {
        throw "Trusted evidence $Label is not a canonical relative path."
    }
    $parts = @($Value.Split('/'))
    if ($parts.Count -eq 0) {
        throw "Trusted evidence $Label has no path segments."
    }
    $reserved = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @('aux', 'con', 'nul', 'prn', 'clock$')) {
        $null = $reserved.Add($name)
    }
    for ($number = 1; $number -le 9; $number += 1) {
        $null = $reserved.Add("com$number")
        $null = $reserved.Add("lpt$number")
    }
    foreach ($part in $parts) {
        if ([string]::IsNullOrWhiteSpace($part) -or
            $part -ceq '.' -or
            $part -ceq '..' -or
            $part.EndsWith('.') -or
            $part.EndsWith(' ') -or
            $part.IndexOf([char] 0) -ge 0) {
            throw "Trusted evidence $Label contains an unsafe path segment."
        }
        foreach ($character in $part.ToCharArray()) {
            if ([int] $character -lt 32) {
                throw "Trusted evidence $Label contains a control character."
            }
        }
        $deviceStem = $part.Split('.')[0]
        if ($reserved.Contains($deviceStem)) {
            throw "Trusted evidence $Label contains Windows device name '$part'."
        }
    }
    return [string] $Value
}

function Read-TrustedEvidenceFileOnce {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [object] $RelativePath,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    $relative = Get-TrustedEvidenceCanonicalRelativePath `
        -Value $RelativePath `
        -Label $Label
    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    if (-not (Test-Path -LiteralPath $rootFull -PathType Container)) {
        throw "Trusted evidence root is missing for ${Label}: '$rootFull'."
    }
    $path = [System.IO.Path]::GetFullPath((
        Join-Path $rootFull ($relative -replace '/', '\')
    ))
    if (-not (Test-PathWithin -Root $rootFull -Candidate $path)) {
        throw "Trusted evidence $Label escaped its session root."
    }
    Assert-NoReparseAncestorChain -Root $rootFull -Candidate $path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Trusted evidence $Label file is missing."
    }
    $item = Get-Item -LiteralPath $path -Force
    if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Trusted evidence $Label may not be a reparse point."
    }

    if ($null -eq ('GonieGonieTrustedEvidenceNativeFile' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public static class GonieGonieTrustedEvidenceNativeFile
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle handle,
        out BY_HANDLE_FILE_INFORMATION information);

    public static uint GetLinkCount(SafeFileHandle handle)
    {
        BY_HANDLE_FILE_INFORMATION information;
        if (!GetFileInformationByHandle(handle, out information))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return information.NumberOfLinks;
    }
}
'@
    }

    $stream = $null
    try {
        $stream = [System.IO.FileStream]::new(
            $path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::None)
        if ([GonieGonieTrustedEvidenceNativeFile]::GetLinkCount(
                $stream.SafeFileHandle) -ne 1) {
            throw "Trusted evidence $Label may not be hardlinked."
        }
        if ($stream.Length -gt [int]::MaxValue) {
            throw "Trusted evidence $Label exceeds the release size limit."
        }
        $bytes = [byte[]]::new([int] $stream.Length)
        $offset = 0
        while ($offset -lt $bytes.Length) {
            $read = $stream.Read($bytes, $offset, $bytes.Length - $offset)
            if ($read -le 0) {
                throw "Trusted evidence $Label ended before its declared length."
            }
            $offset += $read
        }
        if ($stream.ReadByte() -ne -1) {
            throw "Trusted evidence $Label changed while it was read."
        }
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
    Assert-NoReparseAncestorChain -Root $rootFull -Candidate $path
    return [pscustomobject] [ordered] @{
        bytes = [byte[]] $bytes
        length = [long] $bytes.Length
        path = $path
        relativePath = $relative
        sha256 = Get-TrustedEvidenceCanonicalBytesSha256 -Bytes $bytes
    }
}

function Read-TrustedEvidenceJsonOnce {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [object] $RelativePath,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    $read = Read-TrustedEvidenceFileOnce `
        -Root $Root `
        -RelativePath $RelativePath `
        -Label $Label
    $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
    try {
        $text = $strictUtf8.GetString($read.bytes)
        Assert-NoDuplicateJsonObjectKeys -Text $text
        $json = $text | ConvertFrom-Json
    }
    catch {
        throw "Trusted evidence $Label is not strict UTF-8 JSON: $($_.Exception.Message)"
    }
    $read | Add-Member -NotePropertyName json -NotePropertyValue $json
    return $read
}

function Get-TrustedEvidenceArtifactDescriptor {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    Assert-TrustedEvidenceExactProperties `
        -Value $Value `
        -Expected @('bytes', 'path', 'sha256') `
        -Label $Label
    $length = Get-JsonInteger -Value $Value.bytes -Label "$Label.bytes"
    if ($length -lt 0) {
        throw "Trusted evidence $Label byte count cannot be negative."
    }
    return [pscustomobject] [ordered] @{
        bytes = $length
        path = Get-TrustedEvidenceCanonicalRelativePath `
            -Value $Value.path `
            -Label "$Label.path"
        sha256 = Assert-TrustedEvidenceCanonicalHash `
            -Value $Value.sha256 `
            -Label "$Label.sha256"
    }
}

function Add-TrustedEvidenceExpectedArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [object] $ExpectedByPath,

        [Parameter(Mandatory = $true)]
        [string] $Kind,

        [Parameter(Mandatory = $true)]
        [object] $Descriptor,

        [AllowNull()]
        [string] $ProjectPath,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    $normalized = Get-TrustedEvidenceArtifactDescriptor `
        -Value $Descriptor `
        -Label $Label
    if ($ExpectedByPath.ContainsKey($normalized.path)) {
        throw "Trusted evidence expected artifact path '$($normalized.path)' is duplicated."
    }
    $normalizedProjectPath = $null
    if (-not [string]::IsNullOrEmpty($ProjectPath)) {
        $normalizedProjectPath = Get-TrustedEvidenceCanonicalRelativePath `
            -Value $ProjectPath `
            -Label "$Label.project_path"
    }
    $ExpectedByPath.Add(
        $normalized.path,
        [pscustomobject] [ordered] @{
            bytes = $normalized.bytes
            kind = $Kind
            path = $normalized.path
            project_path = $normalizedProjectPath
            sha256 = $normalized.sha256
        })
}

function Assert-TrustedEvidenceReadMatchesDescriptor {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Read,

        [Parameter(Mandatory = $true)]
        [object] $Descriptor,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if ($Read.relativePath -cne $Descriptor.path -or
        $Read.length -ne $Descriptor.bytes -or
        $Read.sha256 -cne $Descriptor.sha256) {
        throw "Trusted evidence $Label bytes do not match their exact index descriptor."
    }
}

function Assert-TrustedEvidenceIndexEntryMatches {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Actual,

        [Parameter(Mandatory = $true)]
        [object] $Expected,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if ($Actual.bytes -ne $Expected.bytes -or
        $Actual.kind -cne $Expected.kind -or
        $Actual.path -cne $Expected.path -or
        $Actual.sha256 -cne $Expected.sha256 -or
        (($null -eq $Actual.project_path) -ne ($null -eq $Expected.project_path)) -or
        ($null -ne $Actual.project_path -and
            $Actual.project_path -cne $Expected.project_path)) {
        throw "Trusted evidence $Label differs from the canonical request/child artifact closure."
    }
}

function Get-TrustedEvidenceCanonicalReceiptMap {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Manifest,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedContentSha256,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedInventorySha256,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedUpstreamCommit,

        [Parameter(Mandatory = $true)]
        [object] $ExpectedAssertionIds
    )

    Assert-TrustedEvidenceExactProperties `
        -Value $Manifest `
        -Expected @(
            'content_sha256',
            'entries',
            'inventory_sha256',
            'schema',
            'summary',
            'upstream_commit') `
        -Label 'tracked symbol-evidence manifest'
    if ($Manifest.schema -isnot [string] -or
        $Manifest.schema -cne 'goniegonie.upstream-symbol-evidence.v1' -or
        $Manifest.entries -isnot [System.Array] -or
        $Manifest.upstream_commit -isnot [string] -or
        $Manifest.upstream_commit -cne $ExpectedUpstreamCommit) {
        throw 'Tracked symbol-evidence manifest identity/schema is invalid.'
    }
    $contentHash = Assert-TrustedEvidenceCanonicalHash `
        -Value $Manifest.content_sha256 `
        -Label 'tracked symbol-evidence content_sha256'
    $inventoryHash = Assert-TrustedEvidenceCanonicalHash `
        -Value $Manifest.inventory_sha256 `
        -Label 'tracked symbol-evidence inventory_sha256'
    if ($contentHash -cne $ExpectedContentSha256 -or
        $inventoryHash -cne $ExpectedInventorySha256) {
        throw 'Tracked symbol-evidence manifest differs from the release bindings.'
    }

    $receiptsById = [System.Collections.Generic.Dictionary[string, object]]::new(
        [System.StringComparer]::Ordinal)
    $entryCount = 0L
    $receiptCount = 0L
    $passedCount = 0L
    $skippedCount = 0L
    $structuralOnlyCount = 0L
    $zeroLoadActiveCount = 0L
    foreach ($entry in $Manifest.entries) {
        Assert-TrustedEvidenceExactProperties `
            -Value $entry `
            -Expected @(
                'implementation',
                'path',
                'receipts',
                'symbol',
                'upstream_symbol_hash') `
            -Label 'tracked symbol-evidence entry'
        Assert-TrustedEvidenceExactProperties `
            -Value $entry.implementation `
            -Expected @('path', 'source_sha256', 'symbol') `
            -Label 'tracked symbol-evidence implementation'
        if ($entry.receipts -isnot [System.Array] -or
            $entry.path -isnot [string] -or
            $entry.symbol -isnot [string] -or
            $entry.implementation.path -isnot [string] -or
            $entry.implementation.symbol -isnot [string] -or
            $entry.symbol -cnotmatch '^[A-Za-z_][A-Za-z0-9_]*(?:(?:\.|::)[A-Za-z_][A-Za-z0-9_]*)*$' -or
            $entry.implementation.symbol -cnotmatch '^[A-Za-z_][A-Za-z0-9_]*(?:(?:\.|::)[A-Za-z_][A-Za-z0-9_]*)*$') {
            throw 'Tracked symbol-evidence entry is malformed.'
        }
        $null = Get-TrustedEvidenceCanonicalRelativePath `
            -Value $entry.path `
            -Label 'tracked symbol-evidence entry.path'
        $null = Get-TrustedEvidenceCanonicalRelativePath `
            -Value $entry.implementation.path `
            -Label 'tracked symbol-evidence implementation.path'
        $null = Assert-TrustedEvidenceCanonicalHash `
            -Value $entry.upstream_symbol_hash `
            -Label 'tracked symbol-evidence upstream_symbol_hash'
        $null = Assert-TrustedEvidenceCanonicalHash `
            -Value $entry.implementation.source_sha256 `
            -Label 'tracked symbol-evidence implementation.source_sha256'
        $entryCount += 1

        foreach ($receipt in $entry.receipts) {
            Assert-TrustedEvidenceExactProperties `
                -Value $receipt `
                -Expected @(
                    'assertion',
                    'claims_active_load',
                    'exercised_load',
                    'expected_output_sha256',
                    'id',
                    'outcome',
                    'skipped',
                    'structural_only',
                    'test_path',
                    'test_source_sha256',
                    'test_symbol',
                    'verification_kind') `
                -Label 'tracked symbol-evidence receipt'
            if ($receipt.id -isnot [string] -or
                $receipt.id -cnotmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$' -or
                $receipt.assertion -isnot [string] -or
                $receipt.outcome -isnot [string] -or
                $receipt.outcome -cne 'passed' -or
                $receipt.skipped -isnot [bool] -or
                $receipt.skipped -ne $false -or
                $receipt.structural_only -isnot [bool] -or
                $receipt.structural_only -ne $false -or
                $receipt.claims_active_load -isnot [bool] -or
                $receipt.exercised_load -isnot [string] -or
                @('not_applicable', 'zero', 'nonzero') -cnotcontains $receipt.exercised_load -or
                ($receipt.claims_active_load -and $receipt.exercised_load -cne 'nonzero') -or
                $receipt.verification_kind -isnot [string] -or
                @(
                    'cross_language',
                    'energyplus_integration',
                    'rhino_workflow',
                    'unit_behavior') -cnotcontains $receipt.verification_kind -or
                $receipt.test_symbol -isnot [string] -or
                $receipt.test_symbol -cnotmatch '^[A-Za-z_][A-Za-z0-9_]*(?:(?:\.|::)[A-Za-z_][A-Za-z0-9_]*)*$') {
                throw 'Tracked symbol-evidence receipt is not exact passing behavioral evidence.'
            }
            $null = Get-TrustedEvidenceCanonicalRelativePath `
                -Value $receipt.test_path `
                -Label 'tracked symbol-evidence receipt.test_path'
            $null = Assert-TrustedEvidenceCanonicalHash `
                -Value $receipt.test_source_sha256 `
                -Label 'tracked symbol-evidence receipt.test_source_sha256'
            $null = Assert-TrustedEvidenceCanonicalHash `
                -Value $receipt.expected_output_sha256 `
                -Label 'tracked symbol-evidence receipt.expected_output_sha256'
            if ($receiptsById.ContainsKey([string] $receipt.id)) {
                throw "Tracked symbol-evidence receipt id '$($receipt.id)' is duplicated."
            }
            $receiptsById.Add([string] $receipt.id, $receipt)
            $receiptCount += 1
            $passedCount += 1
            if ($receipt.skipped) {
                $skippedCount += 1
            }
            if ($receipt.structural_only) {
                $structuralOnlyCount += 1
            }
            if ($receipt.claims_active_load -and
                $receipt.exercised_load -cne 'nonzero') {
                $zeroLoadActiveCount += 1
            }
        }
    }

    Assert-TrustedEvidenceExactProperties `
        -Value $Manifest.summary `
        -Expected @(
            'entry_count',
            'passed_receipt_count',
            'receipt_count',
            'skipped_receipt_count',
            'structural_only_receipt_count',
            'zero_load_active_claim_count') `
        -Label 'tracked symbol-evidence summary'
    $summaryExpected = [ordered] @{
        entry_count = $entryCount
        passed_receipt_count = $passedCount
        receipt_count = $receiptCount
        skipped_receipt_count = $skippedCount
        structural_only_receipt_count = $structuralOnlyCount
        zero_load_active_claim_count = $zeroLoadActiveCount
    }
    foreach ($name in $summaryExpected.Keys) {
        $actual = Get-JsonInteger `
            -Value $Manifest.summary.$name `
            -Label "tracked symbol-evidence summary.$name"
        if ($actual -ne $summaryExpected[$name]) {
            throw "Tracked symbol-evidence summary.$name is inconsistent."
        }
    }
    $contentData = [pscustomobject] [ordered] @{
        entries = $Manifest.entries
        inventory_sha256 = [string] $Manifest.inventory_sha256
        upstream_commit = [string] $Manifest.upstream_commit
    }
    if ((Get-TrustedEvidenceCanonicalDataSha256 -Value $contentData) -cne $contentHash) {
        throw 'Tracked symbol-evidence canonical content hash is invalid.'
    }

    if ($ExpectedAssertionIds -isnot [System.Array]) {
        throw 'Tracked symbol-evidence expected assertion ids must be a JSON array.'
    }
    $previousId = $null
    foreach ($identifier in $ExpectedAssertionIds) {
        if ($identifier -isnot [string] -or
            $identifier -cnotmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$' -or
            ($null -ne $previousId -and
                [string]::CompareOrdinal($previousId, $identifier) -ge 0) -or
            -not $receiptsById.ContainsKey([string] $identifier)) {
            throw 'Tracked symbol-evidence does not exactly cover the ordered report assertions.'
        }
        $previousId = [string] $identifier
    }
    return ,$receiptsById
}

function Copy-TrustedEvidenceSession {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string] $ReleaseRoot,

        [Parameter(Mandatory = $true)]
        [string] $TrustedEvidenceReleaseRoot,

        [Parameter(Mandatory = $true)]
        [object] $Trace,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedAuthorityReceiptSha256,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedRepositoryHead,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedUpstreamCommit,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedInventorySha256,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedMatrixSha256,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedSymbolEvidenceSha256,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedTargetFramework,

        [Parameter(Mandatory = $true)]
        [long] $ExpectedAssertionCount,

        [Parameter(Mandatory = $true)]
        [object] $ExpectedAssertionIds
    )

    Assert-TrustedEvidenceExactProperties `
        -Value $Trace `
        -Expected @(
            'artifact_count',
            'artifact_index_path',
            'artifact_index_sha256',
            'assertion_count',
            'authority_receipt_path',
            'authority_receipt_sha256',
            'project_count',
            'session_id') `
        -Label 'result artifact trace'
    $sessionId = [string] $Trace.session_id
    if ($Trace.session_id -isnot [string] -or
        $sessionId -cnotmatch '^[0-9a-f]{32}$') {
        throw 'Trusted evidence result artifact trace has an invalid session id.'
    }
    $projectCount = Get-JsonInteger `
        -Value $Trace.project_count `
        -Label 'result artifact trace.project_count'
    $assertionCount = Get-JsonInteger `
        -Value $Trace.assertion_count `
        -Label 'result artifact trace.assertion_count'
    $artifactCount = Get-JsonInteger `
        -Value $Trace.artifact_count `
        -Label 'result artifact trace.artifact_count'
    if ($projectCount -le 0 -or
        $assertionCount -le 0 -or
        $artifactCount -le 0 -or
        $assertionCount -ne $ExpectedAssertionCount) {
        throw 'Trusted evidence result artifact trace counts are invalid.'
    }
    if ($ExpectedAssertionIds -isnot [System.Array] -or
        @($ExpectedAssertionIds).Count -ne $ExpectedAssertionCount) {
        throw 'Trusted evidence expected assertion ids must be a count-bound JSON array.'
    }
    $traceReceiptHash = Assert-TrustedEvidenceCanonicalHash `
        -Value $Trace.authority_receipt_sha256 `
        -Label 'result artifact trace.authority_receipt_sha256'
    $traceIndexHash = Assert-TrustedEvidenceCanonicalHash `
        -Value $Trace.artifact_index_sha256 `
        -Label 'result artifact trace.artifact_index_sha256'
    $expectedReceiptHash = Assert-TrustedEvidenceCanonicalHash `
        -Value $ExpectedAuthorityReceiptSha256 `
        -Label 'evidence execution result artifact hash'
    $expectedInventoryHash = Assert-TrustedEvidenceCanonicalHash `
        -Value $ExpectedInventorySha256 `
        -Label 'compatibility inventory hash'
    $expectedMatrixHash = Assert-TrustedEvidenceCanonicalHash `
        -Value $ExpectedMatrixSha256 `
        -Label 'compatibility matrix hash'
    $expectedEvidenceHash = Assert-TrustedEvidenceCanonicalHash `
        -Value $ExpectedSymbolEvidenceSha256 `
        -Label 'symbol evidence hash'
    if ($traceReceiptHash -cne $expectedReceiptHash) {
        throw 'Trusted evidence trace receipt hash is not the exact report result hash.'
    }
    $expectedReceiptPath = "temp/u/$sessionId/a.json"
    $expectedIndexPath = "temp/u/$sessionId/i.json"
    if ($Trace.authority_receipt_path -isnot [string] -or
        $Trace.authority_receipt_path -cne $expectedReceiptPath -or
        $Trace.artifact_index_path -isnot [string] -or
        $Trace.artifact_index_path -cne $expectedIndexPath) {
        throw 'Trusted evidence result artifact paths are not canonical session paths.'
    }
    if ($ExpectedRepositoryHead -cnotmatch '^[0-9a-f]{40}$' -or
        $ExpectedUpstreamCommit -cnotmatch '^[0-9a-f]{40}$' -or
        $ExpectedTargetFramework -cne 'net8.0-windows') {
        throw 'Trusted evidence expected release bindings are invalid.'
    }

    $repositoryFull = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    $sessionRoot = [System.IO.Path]::GetFullPath((
        Join-Path $repositoryFull ("temp\u\$sessionId")
    ))
    Assert-NoReparseAncestorChain -Root $repositoryFull -Candidate $sessionRoot
    if (-not (Test-Path -LiteralPath $sessionRoot -PathType Container)) {
        throw "Trusted evidence session '$sessionId' is missing."
    }
    $receiptRead = Read-TrustedEvidenceJsonOnce `
        -Root $sessionRoot `
        -RelativePath 'a.json' `
        -Label 'authority receipt'
    $indexRead = Read-TrustedEvidenceJsonOnce `
        -Root $sessionRoot `
        -RelativePath 'i.json' `
        -Label 'artifact index'
    if ($receiptRead.sha256 -cne $traceReceiptHash -or
        $indexRead.sha256 -cne $traceIndexHash) {
        throw 'Trusted evidence trace hashes do not match the exact receipt/index bytes.'
    }

    $receipt = $receiptRead.json
    Assert-TrustedEvidenceExactProperties `
        -Value $receipt `
        -Expected @(
            'artifact_count',
            'artifact_index_path',
            'artifact_index_sha256',
            'assertion_count',
            'child_result_sha256',
            'collector_source_sha256',
            'dotnet_executable_sha256',
            'evidence_results_sha256',
            'git_executable_sha256',
            'inventory_sha256',
            'matrix_sha256',
            'project_count',
            'repository_head',
            'request_sha256',
            'schema',
            'session_id',
            'source_tree_sha256',
            'symbol_evidence_sha256',
            'target_framework',
            'toolchain_manifest_sha256',
            'upstream_commit') `
        -Label 'authority receipt'
    if ($receipt.schema -isnot [string] -or
        $receipt.schema -cne 'goniegonie.trusted-evidence-authority-receipt.v1' -or
        $receipt.session_id -isnot [string] -or
        $receipt.session_id -cne $sessionId -or
        $receipt.artifact_index_path -isnot [string] -or
        $receipt.artifact_index_path -cne 'i.json' -or
        $receipt.target_framework -isnot [string] -or
        $receipt.target_framework -cne $ExpectedTargetFramework -or
        $receipt.repository_head -isnot [string] -or
        $receipt.repository_head -cne $ExpectedRepositoryHead -or
        $receipt.upstream_commit -isnot [string] -or
        $receipt.upstream_commit -cne $ExpectedUpstreamCommit) {
        throw 'Trusted evidence authority receipt identity is stale or malformed.'
    }
    $receiptProjectCount = Get-JsonInteger `
        -Value $receipt.project_count `
        -Label 'authority receipt.project_count'
    $receiptAssertionCount = Get-JsonInteger `
        -Value $receipt.assertion_count `
        -Label 'authority receipt.assertion_count'
    $receiptArtifactCount = Get-JsonInteger `
        -Value $receipt.artifact_count `
        -Label 'authority receipt.artifact_count'
    if ($receiptProjectCount -ne $projectCount -or
        $receiptAssertionCount -ne $assertionCount -or
        $receiptArtifactCount -ne $artifactCount) {
        throw 'Trusted evidence authority receipt counts differ from its trace.'
    }
    $receiptHashes = @(
        'artifact_index_sha256',
        'child_result_sha256',
        'collector_source_sha256',
        'dotnet_executable_sha256',
        'evidence_results_sha256',
        'git_executable_sha256',
        'inventory_sha256',
        'matrix_sha256',
        'request_sha256',
        'source_tree_sha256',
        'symbol_evidence_sha256',
        'toolchain_manifest_sha256')
    foreach ($name in $receiptHashes) {
        $null = Assert-TrustedEvidenceCanonicalHash `
            -Value $receipt.$name `
            -Label "authority receipt.$name"
    }
    if ($receipt.artifact_index_sha256 -cne $traceIndexHash -or
        $receipt.inventory_sha256 -cne $expectedInventoryHash -or
        $receipt.matrix_sha256 -cne $expectedMatrixHash -or
        $receipt.symbol_evidence_sha256 -cne $expectedEvidenceHash) {
        throw 'Trusted evidence authority receipt differs from compatibility report bindings.'
    }

    $index = $indexRead.json
    Assert-TrustedEvidenceExactProperties `
        -Value $index `
        -Expected @(
            'artifact_count',
            'artifacts',
            'assertion_count',
            'child_result_sha256',
            'dotnet_executable_sha256',
            'git_executable_sha256',
            'project_count',
            'repository_head',
            'request_sha256',
            'schema',
            'session_id',
            'source_tree_sha256',
            'target_framework',
            'toolchain_manifest_sha256') `
        -Label 'artifact index'
    if ($index.schema -isnot [string] -or
        $index.schema -cne 'goniegonie.trusted-evidence-artifact-index.v1' -or
        $index.session_id -isnot [string] -or
        $index.session_id -cne $sessionId -or
        $index.target_framework -isnot [string] -or
        $index.target_framework -cne $ExpectedTargetFramework -or
        $index.repository_head -isnot [string] -or
        $index.repository_head -cne $ExpectedRepositoryHead -or
        $index.artifacts -isnot [System.Array]) {
        throw 'Trusted evidence artifact index identity/schema is invalid.'
    }
    $indexProjectCount = Get-JsonInteger `
        -Value $index.project_count `
        -Label 'artifact index.project_count'
    $indexAssertionCount = Get-JsonInteger `
        -Value $index.assertion_count `
        -Label 'artifact index.assertion_count'
    $indexArtifactCount = Get-JsonInteger `
        -Value $index.artifact_count `
        -Label 'artifact index.artifact_count'
    if ($indexProjectCount -ne $projectCount -or
        $indexAssertionCount -ne $assertionCount -or
        $indexArtifactCount -ne $artifactCount -or
        @($index.artifacts).Count -ne $artifactCount) {
        throw 'Trusted evidence artifact index counts are inconsistent.'
    }
    $crossHashes = @(
        'child_result_sha256',
        'dotnet_executable_sha256',
        'git_executable_sha256',
        'request_sha256',
        'source_tree_sha256',
        'toolchain_manifest_sha256')
    foreach ($name in $crossHashes) {
        $null = Assert-TrustedEvidenceCanonicalHash `
            -Value $index.$name `
            -Label "artifact index.$name"
        if ($index.$name -cne $receipt.$name) {
            throw "Trusted evidence receipt/index $name bindings differ."
        }
    }

    $allowedKinds = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($kind in @(
        'request',
        'child_result',
        'generated_build_props',
        'parent_evaluation_build_props',
        'child_evaluation_build_props',
        'parent_validation_build_props',
        'restore_stdout',
        'restore_stderr',
        'stdout',
        'stderr',
        'test_dll',
        'trx',
        'implementation_dll',
        'record')) {
        $null = $allowedKinds.Add($kind)
    }
    $indexByPath = [System.Collections.Generic.Dictionary[string, object]]::new(
        [System.StringComparer]::Ordinal)
    $pathsIgnoringCase = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $normalizedEntries = [System.Collections.Generic.List[object]]::new()
    $previousPath = $null
    foreach ($rawEntry in @($index.artifacts)) {
        if ($null -eq $rawEntry -or $rawEntry -isnot [pscustomobject]) {
            throw 'Trusted evidence artifact index entry must be a JSON object.'
        }
        $kind = $rawEntry.kind
        if ($kind -isnot [string] -or -not $allowedKinds.Contains($kind)) {
            throw 'Trusted evidence artifact index entry kind is not allowed.'
        }
        $isGlobal = $kind -ceq 'request' -or $kind -ceq 'child_result'
        $expectedEntryProperties = if ($isGlobal) {
            @('bytes', 'kind', 'path', 'sha256')
        }
        else {
            @('bytes', 'kind', 'path', 'project_path', 'sha256')
        }
        Assert-TrustedEvidenceExactProperties `
            -Value $rawEntry `
            -Expected $expectedEntryProperties `
            -Label 'artifact index entry'
        $length = Get-JsonInteger `
            -Value $rawEntry.bytes `
            -Label 'artifact index entry.bytes'
        if ($length -lt 0) {
            throw 'Trusted evidence artifact index entry has a negative byte count.'
        }
        $path = Get-TrustedEvidenceCanonicalRelativePath `
            -Value $rawEntry.path `
            -Label 'artifact index entry.path'
        $hash = Assert-TrustedEvidenceCanonicalHash `
            -Value $rawEntry.sha256 `
            -Label 'artifact index entry.sha256'
        $projectPath = $null
        if (-not $isGlobal) {
            $projectPath = Get-TrustedEvidenceCanonicalRelativePath `
                -Value $rawEntry.project_path `
                -Label 'artifact index entry.project_path'
        }
        if ($null -ne $previousPath -and
            [string]::CompareOrdinal($previousPath, $path) -ge 0) {
            throw 'Trusted evidence artifact index paths must be unique and ordinally sorted.'
        }
        if (-not $pathsIgnoringCase.Add($path) -or $indexByPath.ContainsKey($path)) {
            throw "Trusted evidence artifact index has a duplicate or case-colliding path '$path'."
        }
        $entry = [pscustomobject] [ordered] @{
            bytes = $length
            kind = [string] $kind
            path = $path
            project_path = $projectPath
            sha256 = $hash
        }
        $indexByPath.Add($path, $entry)
        $normalizedEntries.Add($entry)
        $previousPath = $path
    }
    if (-not $indexByPath.ContainsKey('q.json') -or
        $indexByPath['q.json'].kind -cne 'request' -or
        -not $indexByPath.ContainsKey('z.json') -or
        $indexByPath['z.json'].kind -cne 'child_result') {
        throw 'Trusted evidence index must identify exact q.json and z.json artifacts.'
    }

    $requestRead = Read-TrustedEvidenceJsonOnce `
        -Root $sessionRoot `
        -RelativePath 'q.json' `
        -Label 'collector request'
    $childRead = Read-TrustedEvidenceJsonOnce `
        -Root $sessionRoot `
        -RelativePath 'z.json' `
        -Label 'collector child result'
    if ($receipt.request_sha256 -cne $requestRead.sha256 -or
        $index.request_sha256 -cne $requestRead.sha256) {
        throw 'Trusted evidence receipt/index request hashes do not match actual q.json bytes.'
    }
    if ($receipt.child_result_sha256 -cne $childRead.sha256 -or
        $index.child_result_sha256 -cne $childRead.sha256) {
        throw 'Trusted evidence receipt/index child-result hashes do not match actual z.json bytes.'
    }
    Assert-TrustedEvidenceReadMatchesDescriptor `
        -Read $requestRead `
        -Descriptor $indexByPath['q.json'] `
        -Label 'collector request'
    Assert-TrustedEvidenceReadMatchesDescriptor `
        -Read $childRead `
        -Descriptor $indexByPath['z.json'] `
        -Label 'collector child result'

    $request = $requestRead.json
    Assert-TrustedEvidenceExactProperties `
        -Value $request `
        -Expected @(
            'assertion_count',
            'dotnet',
            'evidence_binding',
            'git',
            'inputs',
            'nonce',
            'package_locks',
            'project_count',
            'projects',
            'repository_head',
            'repository_root',
            'required_assertion_ids',
            'schema',
            'session_directory',
            'session_id',
            'source',
            'target_framework') `
        -Label 'collector request'
    if ($request.schema -isnot [string] -or
        $request.schema -cne 'goniegonie.trusted-evidence-request.v1' -or
        $request.session_id -isnot [string] -or
        $request.session_id -cne $sessionId -or
        $request.repository_head -isnot [string] -or
        $request.repository_head -cne $ExpectedRepositoryHead -or
        $request.target_framework -isnot [string] -or
        $request.target_framework -cne $ExpectedTargetFramework -or
        $request.nonce -isnot [string] -or
        $request.nonce -cnotmatch '^[0-9a-f]{64}$' -or
        $request.projects -isnot [System.Array] -or
        $request.required_assertion_ids -isnot [System.Array] -or
        $request.inputs -isnot [System.Array] -or
        $request.package_locks -isnot [System.Array]) {
        throw 'Trusted evidence collector request identity/schema is invalid.'
    }
    try {
        $requestRepositoryRoot = [System.IO.Path]::GetFullPath(
            [string] $request.repository_root).TrimEnd('\', '/')
        $requestSessionRoot = [System.IO.Path]::GetFullPath(
            [string] $request.session_directory).TrimEnd('\', '/')
    }
    catch {
        throw 'Trusted evidence collector request contains an invalid absolute root.'
    }
    if (-not $requestRepositoryRoot.Equals(
            $repositoryFull,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $requestSessionRoot.Equals(
            $sessionRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Trusted evidence collector request escaped its exact repository/session.'
    }
    $requestProjectCount = Get-JsonInteger `
        -Value $request.project_count `
        -Label 'collector request.project_count'
    $requestAssertionCount = Get-JsonInteger `
        -Value $request.assertion_count `
        -Label 'collector request.assertion_count'
    if ($requestProjectCount -ne $projectCount -or
        @($request.projects).Count -ne $projectCount -or
        $requestAssertionCount -ne $assertionCount -or
        @($request.required_assertion_ids).Count -ne $assertionCount) {
        throw 'Trusted evidence collector request counts are inconsistent.'
    }
    Assert-TrustedEvidenceExactProperties `
        -Value $request.evidence_binding `
        -Expected @(
            'collector_path',
            'collector_source_sha256',
            'collector_symbol',
            'inventory_sha256',
            'matrix_sha256',
            'symbol_evidence_sha256',
            'upstream_commit') `
        -Label 'collector request.evidence_binding'
    foreach ($name in @(
        'collector_source_sha256',
        'inventory_sha256',
        'matrix_sha256',
        'symbol_evidence_sha256')) {
        $null = Assert-TrustedEvidenceCanonicalHash `
            -Value $request.evidence_binding.$name `
            -Label "collector request.evidence_binding.$name"
    }
    if ($request.evidence_binding.collector_path -isnot [string] -or
        $request.evidence_binding.collector_path -cne 'tools/upstream-tracker/goniegonie_upstream_tracker/trusted_collector.py' -or
        $request.evidence_binding.collector_symbol -isnot [string] -or
        $request.evidence_binding.collector_symbol -cne 'collect_trusted_evidence' -or
        $request.evidence_binding.collector_source_sha256 -cne $receipt.collector_source_sha256 -or
        $request.evidence_binding.inventory_sha256 -cne $expectedInventoryHash -or
        $request.evidence_binding.matrix_sha256 -cne $expectedMatrixHash -or
        $request.evidence_binding.symbol_evidence_sha256 -cne $expectedEvidenceHash -or
        $request.evidence_binding.upstream_commit -isnot [string] -or
        $request.evidence_binding.upstream_commit -cne $ExpectedUpstreamCommit) {
        throw 'Trusted evidence request manifest bindings differ from the release report.'
    }
    Assert-TrustedEvidenceExactProperties `
        -Value $request.dotnet `
        -Expected @('path', 'sdk_manifest', 'sdk_root', 'sdk_version', 'sha256') `
        -Label 'collector request.dotnet'
    Assert-TrustedEvidenceExactProperties `
        -Value $request.dotnet.sdk_manifest `
        -Expected @('file_count', 'files', 'root', 'schema', 'sha256') `
        -Label 'collector request.dotnet.sdk_manifest'
    if ($request.dotnet.path -isnot [string] -or
        $request.dotnet.sdk_root -isnot [string] -or
        $request.dotnet.sdk_version -isnot [string] -or
        $request.dotnet.sdk_manifest.schema -isnot [string] -or
        $request.dotnet.sdk_manifest.schema -cne 'goniegonie.trusted-dotnet-sdk-manifest.v1' -or
        $request.dotnet.sdk_manifest.files -isnot [System.Array]) {
        throw 'Trusted evidence request dotnet/SDK manifest shape is invalid.'
    }
    $null = Assert-TrustedEvidenceCanonicalHash `
        -Value $request.dotnet.sha256 `
        -Label 'collector request.dotnet.sha256'
    $null = Assert-TrustedEvidenceCanonicalHash `
        -Value $request.dotnet.sdk_manifest.sha256 `
        -Label 'collector request.dotnet.sdk_manifest.sha256'
    $sdkFileCount = Get-JsonInteger `
        -Value $request.dotnet.sdk_manifest.file_count `
        -Label 'collector request.dotnet.sdk_manifest.file_count'
    if ($sdkFileCount -le 0 -or
        @($request.dotnet.sdk_manifest.files).Count -ne $sdkFileCount -or
        $request.dotnet.sha256 -cne $receipt.dotnet_executable_sha256 -or
        $request.dotnet.sdk_manifest.sha256 -cne $receipt.toolchain_manifest_sha256) {
        throw 'Trusted evidence request dotnet/SDK bindings are inconsistent.'
    }
    Assert-TrustedEvidenceExactProperties `
        -Value $request.git `
        -Expected @('path', 'sha256') `
        -Label 'collector request.git'
    if ($request.git.path -isnot [string]) {
        throw 'Trusted evidence request git path must be text.'
    }
    $null = Assert-TrustedEvidenceCanonicalHash `
        -Value $request.git.sha256 `
        -Label 'collector request.git.sha256'
    if ($request.git.sha256 -cne $receipt.git_executable_sha256) {
        throw 'Trusted evidence request git binding differs from its receipt.'
    }
    Assert-TrustedEvidenceExactProperties `
        -Value $request.source `
        -Expected @('file_count', 'files', 'root', 'sha256') `
        -Label 'collector request.source'
    if ($request.source.root -isnot [string] -or
        $request.source.files -isnot [System.Array]) {
        throw 'Trusted evidence request source tree shape is invalid.'
    }
    try {
        $requestSourceRoot = [System.IO.Path]::GetFullPath(
            [string] $request.source.root).TrimEnd('\', '/')
    }
    catch {
        throw 'Trusted evidence request source root is invalid.'
    }
    $expectedSourceRoot = [System.IO.Path]::GetFullPath(
        (Join-Path $sessionRoot 's')).TrimEnd('\', '/')
    $sourceFileCount = Get-JsonInteger `
        -Value $request.source.file_count `
        -Label 'collector request.source.file_count'
    $null = Assert-TrustedEvidenceCanonicalHash `
        -Value $request.source.sha256 `
        -Label 'collector request.source.sha256'
    if (-not $requestSourceRoot.Equals(
            $expectedSourceRoot,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        $sourceFileCount -le 0 -or
        @($request.source.files).Count -ne $sourceFileCount -or
        $request.source.sha256 -cne $receipt.source_tree_sha256) {
        throw 'Trusted evidence request source-tree binding is inconsistent.'
    }

    $sourcePathsIgnoringCase = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $normalizedSourceFiles = [System.Collections.Generic.List[object]]::new()
    $sourceManifestDescriptor = $null
    $previousSourcePath = $null
    foreach ($rawSourceFile in $request.source.files) {
        Assert-TrustedEvidenceExactProperties `
            -Value $rawSourceFile `
            -Expected @('path', 'sha256') `
            -Label 'collector request source file'
        $sourcePath = Get-TrustedEvidenceCanonicalRelativePath `
            -Value $rawSourceFile.path `
            -Label 'collector request source file.path'
        $sourceHash = Assert-TrustedEvidenceCanonicalHash `
            -Value $rawSourceFile.sha256 `
            -Label 'collector request source file.sha256'
        if (-not $sourcePathsIgnoringCase.Add($sourcePath) -or
            ($null -ne $previousSourcePath -and
                [string]::CompareOrdinal($previousSourcePath, $sourcePath) -ge 0)) {
            throw 'Trusted evidence request source files must be unique and ordinally sorted.'
        }
        $normalizedSource = [pscustomobject] [ordered] @{
            path = $sourcePath
            sha256 = $sourceHash
        }
        $normalizedSourceFiles.Add($normalizedSource)
        if ($sourcePath -ceq 'upstream/symbol-evidence.json') {
            $sourceManifestDescriptor = $normalizedSource
        }
        $previousSourcePath = $sourcePath
    }
    $sourceContent = [pscustomobject] [ordered] @{
        files = @($normalizedSourceFiles)
    }
    if ((Get-TrustedEvidenceCanonicalDataSha256 -Value $sourceContent) -cne
        $request.source.sha256 -or
        $null -eq $sourceManifestDescriptor) {
        throw 'Trusted evidence request source descriptor is not canonical or lacks symbol evidence.'
    }

    $inputPathsIgnoringCase = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $normalizedInputs = [System.Collections.Generic.List[object]]::new()
    $inputManifestDescriptor = $null
    $previousInputPath = $null
    foreach ($rawInput in $request.inputs) {
        Assert-TrustedEvidenceExactProperties `
            -Value $rawInput `
            -Expected @('path', 'sha256') `
            -Label 'collector request input'
        $inputPath = Get-TrustedEvidenceCanonicalRelativePath `
            -Value $rawInput.path `
            -Label 'collector request input.path'
        $inputHash = Assert-TrustedEvidenceCanonicalHash `
            -Value $rawInput.sha256 `
            -Label 'collector request input.sha256'
        if (-not $inputPathsIgnoringCase.Add($inputPath) -or
            ($null -ne $previousInputPath -and
                [string]::CompareOrdinal($previousInputPath, $inputPath) -ge 0)) {
            throw 'Trusted evidence request inputs must be unique and ordinally sorted.'
        }
        $normalizedInput = [pscustomobject] [ordered] @{
            path = $inputPath
            sha256 = $inputHash
        }
        $normalizedInputs.Add($normalizedInput)
        if ($inputPath -ceq 'upstream/symbol-evidence.json') {
            $inputManifestDescriptor = $normalizedInput
        }
        $previousInputPath = $inputPath
    }
    if ($null -eq $inputManifestDescriptor -or
        $inputManifestDescriptor.sha256 -cne $sourceManifestDescriptor.sha256) {
        throw 'Trusted evidence request input closure does not bind tracked symbol evidence.'
    }
    $normalizedPackageLocks = [System.Collections.Generic.List[object]]::new()
    $previousLockPath = $null
    foreach ($rawLock in $request.package_locks) {
        Assert-TrustedEvidenceExactProperties `
            -Value $rawLock `
            -Expected @('path', 'sha256') `
            -Label 'collector request package lock'
        $lockPath = Get-TrustedEvidenceCanonicalRelativePath `
            -Value $rawLock.path `
            -Label 'collector request package lock.path'
        $lockHash = Assert-TrustedEvidenceCanonicalHash `
            -Value $rawLock.sha256 `
            -Label 'collector request package lock.sha256'
        if ($null -ne $previousLockPath -and
            [string]::CompareOrdinal($previousLockPath, $lockPath) -ge 0) {
            throw 'Trusted evidence request package locks must be unique and sorted.'
        }
        $matchingInput = @($normalizedInputs | Where-Object {
            $_.path -ceq $lockPath -and $_.sha256 -ceq $lockHash
        })
        if ($matchingInput.Count -ne 1 -or
            [System.IO.Path]::GetFileName($lockPath) -cne 'packages.lock.json') {
            throw 'Trusted evidence request package-lock closure is not exact.'
        }
        $normalizedPackageLocks.Add([pscustomobject] [ordered] @{
            path = $lockPath
            sha256 = $lockHash
        })
        $previousLockPath = $lockPath
    }
    $expectedPackageLocks = @($normalizedInputs | Where-Object {
        [System.IO.Path]::GetFileName($_.path) -ceq 'packages.lock.json'
    })
    if ((ConvertTo-TrustedEvidenceCanonicalJson -Value @($normalizedPackageLocks)) -cne
        (ConvertTo-TrustedEvidenceCanonicalJson -Value $expectedPackageLocks)) {
        throw 'Trusted evidence request omits or adds a package-lock input.'
    }

    $repositoryEvidenceRead = Read-TrustedEvidenceJsonOnce `
        -Root $repositoryFull `
        -RelativePath 'upstream/symbol-evidence.json' `
        -Label 'tracked repository symbol evidence'
    $sourceEvidenceRead = Read-TrustedEvidenceJsonOnce `
        -Root $expectedSourceRoot `
        -RelativePath 'upstream/symbol-evidence.json' `
        -Label 'materialized symbol evidence'
    if ($repositoryEvidenceRead.sha256 -cne $sourceManifestDescriptor.sha256 -or
        $sourceEvidenceRead.sha256 -cne $sourceManifestDescriptor.sha256 -or
        $repositoryEvidenceRead.length -ne $sourceEvidenceRead.length) {
        throw 'Tracked and materialized symbol-evidence bytes differ from the clean source descriptor.'
    }
    $canonicalReceipts = Get-TrustedEvidenceCanonicalReceiptMap `
        -Manifest $repositoryEvidenceRead.json `
        -ExpectedContentSha256 $expectedEvidenceHash `
        -ExpectedInventorySha256 $expectedInventoryHash `
        -ExpectedUpstreamCommit $ExpectedUpstreamCommit `
        -ExpectedAssertionIds $ExpectedAssertionIds

    $requiredAssertionIds = [System.Collections.Generic.List[string]]::new()
    $previousAssertionId = $null
    $assertionPosition = 0
    foreach ($identifier in @($request.required_assertion_ids)) {
        if ($identifier -isnot [string] -or
            $identifier -cnotmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$' -or
            ($null -ne $previousAssertionId -and
                [string]::CompareOrdinal($previousAssertionId, $identifier) -ge 0) -or
            $identifier -cne $ExpectedAssertionIds[$assertionPosition]) {
            throw 'Trusted evidence request assertion ids must be canonical, unique, and sorted.'
        }
        $requiredAssertionIds.Add([string] $identifier)
        $previousAssertionId = [string] $identifier
        $assertionPosition += 1
    }

    $expectedByPath = [System.Collections.Generic.Dictionary[string, object]]::new(
        [System.StringComparer]::Ordinal)
    Add-TrustedEvidenceExpectedArtifact `
        -ExpectedByPath $expectedByPath `
        -Kind 'request' `
        -Descriptor ([pscustomobject] [ordered] @{
            bytes = $requestRead.length
            path = 'q.json'
            sha256 = $requestRead.sha256
        }) `
        -ProjectPath $null `
        -Label 'collector request artifact'
    Add-TrustedEvidenceExpectedArtifact `
        -ExpectedByPath $expectedByPath `
        -Kind 'child_result' `
        -Descriptor ([pscustomobject] [ordered] @{
            bytes = $childRead.length
            path = 'z.json'
            sha256 = $childRead.sha256
        }) `
        -ProjectPath $null `
        -Label 'collector child-result artifact'

    $requestProjectsByPath = [System.Collections.Generic.Dictionary[string, object]]::new(
        [System.StringComparer]::Ordinal)
    $projectPathsIgnoringCase = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $plannedAssertionIds = [System.Collections.Generic.List[string]]::new()
    $requestAssertionsById = [System.Collections.Generic.Dictionary[string, object]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($project in @($request.projects)) {
        Assert-TrustedEvidenceExactProperties `
            -Value $project `
            -Expected @(
                'arguments',
                'assembly_name',
                'assertions',
                'build_props',
                'evaluated_graph',
                'implementation_assemblies',
                'path',
                'planning_build_props',
                'restore_arguments',
                'slug') `
            -Label 'collector request project'
        $projectPath = Get-TrustedEvidenceCanonicalRelativePath `
            -Value $project.path `
            -Label 'collector request project.path'
        if (-not $projectPathsIgnoringCase.Add($projectPath) -or
            $requestProjectsByPath.ContainsKey($projectPath) -or
            $project.slug -isnot [string] -or
            $project.slug -cnotmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$' -or
            $project.arguments -isnot [System.Array] -or
            $project.restore_arguments -isnot [System.Array] -or
            $project.assertions -isnot [System.Array] -or
            $project.implementation_assemblies -isnot [System.Array]) {
            throw 'Trusted evidence request project is duplicate or malformed.'
        }
        foreach ($argument in $project.arguments) {
            if ($argument -isnot [string]) {
                throw 'Trusted evidence request project arguments must be exact text.'
            }
        }
        foreach ($argument in $project.restore_arguments) {
            if ($argument -isnot [string]) {
                throw 'Trusted evidence request project restore arguments must be exact text.'
            }
        }
        foreach ($assertion in @($project.assertions)) {
            Assert-TrustedEvidenceExactProperties `
                -Value $assertion `
                -Expected @(
                    'exercised_load',
                    'id',
                    'test_path',
                    'test_source_sha256',
                    'test_symbol') `
                -Label 'collector request assertion'
            if ($assertion.id -isnot [string] -or
                -not $canonicalReceipts.ContainsKey([string] $assertion.id) -or
                $requestAssertionsById.ContainsKey([string] $assertion.id)) {
                throw 'Trusted evidence request assertion id is missing, duplicate, or noncanonical.'
            }
            $canonicalReceipt = $canonicalReceipts[[string] $assertion.id]
            $null = Assert-TrustedEvidenceCanonicalHash `
                -Value $assertion.test_source_sha256 `
                -Label 'collector request assertion.test_source_sha256'
            $null = Get-TrustedEvidenceCanonicalRelativePath `
                -Value $assertion.test_path `
                -Label 'collector request assertion.test_path'
            if ($assertion.exercised_load -isnot [string] -or
                $assertion.test_symbol -isnot [string] -or
                $assertion.exercised_load -cne $canonicalReceipt.exercised_load -or
                $assertion.test_path -cne $canonicalReceipt.test_path -or
                $assertion.test_source_sha256 -cne $canonicalReceipt.test_source_sha256 -or
                $assertion.test_symbol -cne $canonicalReceipt.test_symbol) {
                throw 'Trusted evidence request assertion differs from its canonical receipt.'
            }
            $requestAssertionsById.Add([string] $assertion.id, $assertion)
            $plannedAssertionIds.Add([string] $assertion.id)
        }
        $requestProjectsByPath.Add($projectPath, $project)
        Add-TrustedEvidenceExpectedArtifact `
            -ExpectedByPath $expectedByPath `
            -Kind 'generated_build_props' `
            -Descriptor $project.build_props `
            -ProjectPath $projectPath `
            -Label 'generated build props'
        Add-TrustedEvidenceExpectedArtifact `
            -ExpectedByPath $expectedByPath `
            -Kind 'parent_evaluation_build_props' `
            -Descriptor $project.planning_build_props `
            -ProjectPath $projectPath `
            -Label 'parent evaluation build props'
    }
    $plannedArray = @($plannedAssertionIds | Sort-Object)
    if ($plannedArray.Count -ne $requiredAssertionIds.Count) {
        throw 'Trusted evidence request project assertions do not match the required count.'
    }
    for ($position = 0; $position -lt $requiredAssertionIds.Count; $position += 1) {
        if ($plannedArray[$position] -cne $requiredAssertionIds[$position]) {
            throw 'Trusted evidence request project assertions do not match required ids.'
        }
    }

    $child = $childRead.json
    Assert-TrustedEvidenceExactProperties `
        -Value $child `
        -Expected @(
            'artifact_count',
            'assertion_count',
            'assertions',
            'git_executable_sha256',
            'inputs',
            'nonce',
            'package_locks',
            'project_count',
            'projects',
            'repository_head',
            'request_sha256',
            'schema',
            'session_id',
            'source_tree_sha256',
            'target_framework',
            'toolchain_manifest_sha256') `
        -Label 'collector child result'
    if ($child.schema -isnot [string] -or
        $child.schema -cne 'goniegonie.trusted-evidence-child-result.v1' -or
        $child.session_id -isnot [string] -or
        $child.session_id -cne $sessionId -or
        $child.repository_head -isnot [string] -or
        $child.repository_head -cne $ExpectedRepositoryHead -or
        $child.target_framework -isnot [string] -or
        $child.target_framework -cne $ExpectedTargetFramework -or
        $child.nonce -isnot [string] -or
        $child.nonce -cne $request.nonce -or
        $child.projects -isnot [System.Array] -or
        $child.assertions -isnot [System.Array] -or
        $child.inputs -isnot [System.Array] -or
        $child.package_locks -isnot [System.Array]) {
        throw 'Trusted evidence collector child result identity/schema is invalid.'
    }
    if ((ConvertTo-TrustedEvidenceCanonicalJson -Value $child.inputs) -cne
            (ConvertTo-TrustedEvidenceCanonicalJson -Value $request.inputs) -or
        (ConvertTo-TrustedEvidenceCanonicalJson -Value $child.package_locks) -cne
            (ConvertTo-TrustedEvidenceCanonicalJson -Value $request.package_locks)) {
        throw 'Trusted evidence child input/package-lock closure differs from q.json.'
    }
    $childProjectCount = Get-JsonInteger `
        -Value $child.project_count `
        -Label 'collector child result.project_count'
    $childAssertionCount = Get-JsonInteger `
        -Value $child.assertion_count `
        -Label 'collector child result.assertion_count'
    $childArtifactCount = Get-JsonInteger `
        -Value $child.artifact_count `
        -Label 'collector child result.artifact_count'
    foreach ($name in @(
        'git_executable_sha256',
        'request_sha256',
        'source_tree_sha256',
        'toolchain_manifest_sha256')) {
        $null = Assert-TrustedEvidenceCanonicalHash `
            -Value $child.$name `
            -Label "collector child result.$name"
    }
    if ($childProjectCount -ne $projectCount -or
        @($child.projects).Count -ne $projectCount -or
        $childAssertionCount -ne $assertionCount -or
        @($child.assertions).Count -ne $assertionCount -or
        $childArtifactCount -ne $artifactCount -or
        $child.request_sha256 -cne $requestRead.sha256 -or
        $child.git_executable_sha256 -cne $receipt.git_executable_sha256 -or
        $child.source_tree_sha256 -cne $receipt.source_tree_sha256 -or
        $child.toolchain_manifest_sha256 -cne $receipt.toolchain_manifest_sha256) {
        throw 'Trusted evidence child-result counts or cross-bindings are inconsistent.'
    }
    $childAssertionIds = [System.Collections.Generic.List[string]]::new()
    $childAssertionsById = [System.Collections.Generic.Dictionary[string, object]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($assertion in @($child.assertions)) {
        Assert-TrustedEvidenceExactProperties `
            -Value $assertion `
            -Expected @(
                'assertion_id',
                'exercised_load',
                'outcome',
                'output_sha256',
                'skipped',
                'structural_only',
                'test_path',
                'test_source_sha256',
                'test_symbol') `
            -Label 'collector child assertion'
        if ($assertion.assertion_id -isnot [string] -or
            -not $canonicalReceipts.ContainsKey([string] $assertion.assertion_id) -or
            -not $requestAssertionsById.ContainsKey([string] $assertion.assertion_id) -or
            $childAssertionsById.ContainsKey([string] $assertion.assertion_id)) {
            throw 'Trusted evidence child assertion id is missing, duplicate, or noncanonical.'
        }
        $canonicalReceipt = $canonicalReceipts[[string] $assertion.assertion_id]
        $requestAssertion = $requestAssertionsById[[string] $assertion.assertion_id]
        $null = Assert-TrustedEvidenceCanonicalHash `
            -Value $assertion.output_sha256 `
            -Label 'collector child assertion.output_sha256'
        $null = Assert-TrustedEvidenceCanonicalHash `
            -Value $assertion.test_source_sha256 `
            -Label 'collector child assertion.test_source_sha256'
        $null = Get-TrustedEvidenceCanonicalRelativePath `
            -Value $assertion.test_path `
            -Label 'collector child assertion.test_path'
        if ($assertion.outcome -isnot [string] -or
            $assertion.outcome -cne 'passed' -or
            $assertion.skipped -isnot [bool] -or
            $assertion.skipped -ne $false -or
            $assertion.structural_only -isnot [bool] -or
            $assertion.structural_only -ne $false -or
            $assertion.exercised_load -isnot [string] -or
            $assertion.test_symbol -isnot [string] -or
            $assertion.output_sha256 -cne $canonicalReceipt.expected_output_sha256 -or
            $assertion.exercised_load -cne $canonicalReceipt.exercised_load -or
            $assertion.test_path -cne $canonicalReceipt.test_path -or
            $assertion.test_source_sha256 -cne $canonicalReceipt.test_source_sha256 -or
            $assertion.test_symbol -cne $canonicalReceipt.test_symbol -or
            $assertion.exercised_load -cne $requestAssertion.exercised_load -or
            $assertion.test_path -cne $requestAssertion.test_path -or
            $assertion.test_source_sha256 -cne $requestAssertion.test_source_sha256 -or
            $assertion.test_symbol -cne $requestAssertion.test_symbol) {
            throw 'Trusted evidence child assertion is not exact passing canonical evidence.'
        }
        $childAssertionsById.Add([string] $assertion.assertion_id, $assertion)
        $childAssertionIds.Add([string] $assertion.assertion_id)
    }
    for ($position = 0; $position -lt $requiredAssertionIds.Count; $position += 1) {
        if ($childAssertionIds[$position] -cne $requiredAssertionIds[$position]) {
            throw 'Trusted evidence child assertions differ from request assertions.'
        }
    }
    $evidenceResultsContent = [pscustomobject] [ordered] @{
        assertions = $child.assertions
        collector = [pscustomobject] [ordered] @{
            path = [string] $request.evidence_binding.collector_path
            source_sha256 = [string] $request.evidence_binding.collector_source_sha256
            symbol = [string] $request.evidence_binding.collector_symbol
        }
        inventory_sha256 = [string] $request.evidence_binding.inventory_sha256
        symbol_evidence_sha256 = [string] $request.evidence_binding.symbol_evidence_sha256
        target_framework = [string] $request.target_framework
        upstream_commit = [string] $request.evidence_binding.upstream_commit
    }
    if ((Get-TrustedEvidenceCanonicalDataSha256 -Value $evidenceResultsContent) -cne
        $receipt.evidence_results_sha256) {
        throw 'Trusted evidence authority receipt has a forged EvidenceResults content hash.'
    }

    $childProjectAssertionIds = [System.Collections.Generic.List[string]]::new()
    $seenChildProjects = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($project in @($child.projects)) {
        Assert-TrustedEvidenceExactProperties `
            -Value $project `
            -Expected @(
                'arguments',
                'assertions',
                'evaluated_graph',
                'evaluation_build_props',
                'exit_code',
                'implementation_dlls',
                'path',
                'parent_validation_build_props',
                'records',
                'restore_arguments',
                'restore_exit_code',
                'restore_stderr',
                'restore_stdout',
                'stderr',
                'stdout',
                'test_dll',
                'trx') `
            -Label 'collector child project'
        $projectPath = Get-TrustedEvidenceCanonicalRelativePath `
            -Value $project.path `
            -Label 'collector child project.path'
        if (-not $seenChildProjects.Add($projectPath) -or
            -not $requestProjectsByPath.ContainsKey($projectPath) -or
            $project.arguments -isnot [System.Array] -or
            $project.restore_arguments -isnot [System.Array] -or
            $project.assertions -isnot [System.Array] -or
            $project.implementation_dlls -isnot [System.Array] -or
            $project.records -isnot [System.Array]) {
            throw 'Trusted evidence child project closure is invalid.'
        }
        $requestProject = $requestProjectsByPath[$projectPath]
        $projectExitCode = Get-JsonInteger `
            -Value $project.exit_code `
            -Label 'collector child project.exit_code'
        $projectRestoreExitCode = Get-JsonInteger `
            -Value $project.restore_exit_code `
            -Label 'collector child project.restore_exit_code'
        if ($projectExitCode -ne 0 -or
            $projectRestoreExitCode -ne 0 -or
            (ConvertTo-TrustedEvidenceCanonicalJson -Value $project.arguments) -cne
                (ConvertTo-TrustedEvidenceCanonicalJson -Value $requestProject.arguments) -or
            (ConvertTo-TrustedEvidenceCanonicalJson -Value $project.restore_arguments) -cne
                (ConvertTo-TrustedEvidenceCanonicalJson -Value $requestProject.restore_arguments) -or
            (ConvertTo-TrustedEvidenceCanonicalJson -Value $project.evaluated_graph) -cne
                (ConvertTo-TrustedEvidenceCanonicalJson -Value $requestProject.evaluated_graph)) {
            throw 'Trusted evidence child project restore/test command/graph/exit binding is invalid.'
        }
        if (@($project.implementation_dlls).Count -ne
            @($requestProject.implementation_assemblies).Count -or
            @($project.records).Count -lt @($project.assertions).Count) {
            throw 'Trusted evidence child implementation/record counts are inconsistent.'
        }
        foreach ($assertion in @($project.assertions)) {
            Assert-TrustedEvidenceExactProperties `
                -Value $assertion `
                -Expected @(
                    'assertion_id',
                    'exercised_load',
                    'outcome',
                    'output_sha256',
                    'skipped',
                    'structural_only',
                    'test_path',
                    'test_source_sha256',
                    'test_symbol') `
                -Label 'collector child project assertion'
            if ($assertion.assertion_id -isnot [string] -or
                -not $childAssertionsById.ContainsKey([string] $assertion.assertion_id) -or
                (ConvertTo-TrustedEvidenceCanonicalJson -Value $assertion) -cne
                    (ConvertTo-TrustedEvidenceCanonicalJson `
                        -Value $childAssertionsById[[string] $assertion.assertion_id])) {
                throw 'Trusted evidence child project assertion is malformed.'
            }
            $childProjectAssertionIds.Add([string] $assertion.assertion_id)
        }
        Add-TrustedEvidenceExpectedArtifact `
            -ExpectedByPath $expectedByPath `
            -Kind 'child_evaluation_build_props' `
            -Descriptor $project.evaluation_build_props `
            -ProjectPath $projectPath `
            -Label 'child evaluation build props'
        Add-TrustedEvidenceExpectedArtifact `
            -ExpectedByPath $expectedByPath `
            -Kind 'parent_validation_build_props' `
            -Descriptor $project.parent_validation_build_props `
            -ProjectPath $projectPath `
            -Label 'parent validation build props'
        foreach ($name in @(
            'restore_stderr',
            'restore_stdout',
            'stderr',
            'stdout',
            'test_dll',
            'trx')) {
            Add-TrustedEvidenceExpectedArtifact `
                -ExpectedByPath $expectedByPath `
                -Kind $name `
                -Descriptor $project.$name `
                -ProjectPath $projectPath `
                -Label "child $name"
        }
        foreach ($descriptor in @($project.implementation_dlls)) {
            Add-TrustedEvidenceExpectedArtifact `
                -ExpectedByPath $expectedByPath `
                -Kind 'implementation_dll' `
                -Descriptor $descriptor `
                -ProjectPath $projectPath `
                -Label 'child implementation DLL'
        }
        foreach ($descriptor in @($project.records)) {
            Add-TrustedEvidenceExpectedArtifact `
                -ExpectedByPath $expectedByPath `
                -Kind 'record' `
                -Descriptor $descriptor `
                -ProjectPath $projectPath `
                -Label 'child evidence record'
        }
        if ($project.parent_validation_build_props.path -cne
            "g2/$($requestProject.slug)/d.props") {
            throw 'Trusted evidence parent validation build props path is not canonical.'
        }
    }
    $childProjectAssertionArray = @($childProjectAssertionIds | Sort-Object)
    if ($childProjectAssertionArray.Count -ne $requiredAssertionIds.Count) {
        throw 'Trusted evidence child project assertions have the wrong count.'
    }
    for ($position = 0; $position -lt $requiredAssertionIds.Count; $position += 1) {
        if ($childProjectAssertionArray[$position] -cne $requiredAssertionIds[$position]) {
            throw 'Trusted evidence child project assertions differ from request assertions.'
        }
    }
    if ($expectedByPath.Count -ne $artifactCount -or
        $indexByPath.Count -ne $artifactCount) {
        throw 'Trusted evidence index has missing or extra artifact entries.'
    }
    foreach ($entry in $normalizedEntries) {
        if (-not $expectedByPath.ContainsKey($entry.path)) {
            throw "Trusted evidence index contains unexpected artifact '$($entry.path)'."
        }
        Assert-TrustedEvidenceIndexEntryMatches `
            -Actual $entry `
            -Expected $expectedByPath[$entry.path] `
            -Label $entry.path
    }

    $trustedRootFull = [System.IO.Path]::GetFullPath(
        $TrustedEvidenceReleaseRoot).TrimEnd('\', '/')
    $releaseFull = [System.IO.Path]::GetFullPath($ReleaseRoot).TrimEnd('\', '/')
    if (-not (Test-PathWithin -Root $releaseFull -Candidate $trustedRootFull)) {
        throw 'Trusted evidence release root escaped the release staging directory.'
    }
    $null = [System.IO.Directory]::CreateDirectory($trustedRootFull)
    Assert-NoReparseAncestorChain -Root $releaseFull -Candidate $trustedRootFull
    $destinationRoot = Join-Path $trustedRootFull $sessionId
    if (Test-Path -LiteralPath $destinationRoot) {
        throw "Trusted evidence destination session '$sessionId' already exists."
    }
    $null = [System.IO.Directory]::CreateDirectory($destinationRoot)
    Assert-NoReparseAncestorChain -Root $releaseFull -Candidate $destinationRoot
    $artifactDestinationRoot = Join-Path $destinationRoot 'artifacts'
    $null = [System.IO.Directory]::CreateDirectory($artifactDestinationRoot)
    Assert-NoReparseAncestorChain -Root $destinationRoot -Candidate $artifactDestinationRoot

    $copiedArtifacts = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in $normalizedEntries) {
        if ($entry.path -ceq 'q.json') {
            $sourceRead = $requestRead
        }
        elseif ($entry.path -ceq 'z.json') {
            $sourceRead = $childRead
        }
        else {
            $sourceRead = Read-TrustedEvidenceFileOnce `
                -Root $sessionRoot `
                -RelativePath $entry.path `
                -Label "indexed artifact $($entry.path)"
        }
        Assert-TrustedEvidenceReadMatchesDescriptor `
            -Read $sourceRead `
            -Descriptor $entry `
            -Label "indexed artifact $($entry.path)"
        $destination = [System.IO.Path]::GetFullPath((
            Join-Path $artifactDestinationRoot ($entry.path -replace '/', '\')
        ))
        if (-not (Test-PathWithin -Root $artifactDestinationRoot -Candidate $destination)) {
            throw "Trusted evidence destination escaped for '$($entry.path)'."
        }
        $destinationParent = Split-Path -Parent $destination
        $null = [System.IO.Directory]::CreateDirectory($destinationParent)
        Assert-NoReparseAncestorChain `
            -Root $artifactDestinationRoot `
            -Candidate $destinationParent
        Write-BytesAtomicallyExclusive `
            -Path $destination `
            -Bytes $sourceRead.bytes
        $destinationRead = Read-TrustedEvidenceFileOnce `
            -Root $artifactDestinationRoot `
            -RelativePath $entry.path `
            -Label "copied artifact $($entry.path)"
        Assert-TrustedEvidenceReadMatchesDescriptor `
            -Read $destinationRead `
            -Descriptor $entry `
            -Label "copied artifact $($entry.path)"
        $attested = [ordered] @{
            bytes = $entry.bytes
            kind = $entry.kind
            path = 'release/' + (Get-RelativeUnixPath `
                -Root $releaseFull `
                -Path $destination)
            sha256 = $entry.sha256
        }
        if ($null -ne $entry.project_path) {
            $attested.projectPath = $entry.project_path
        }
        $copiedArtifacts.Add([pscustomobject] $attested)
    }

    $receiptDestination = Join-Path $destinationRoot 'authority-receipt.json'
    $indexDestination = Join-Path $destinationRoot 'artifact-index.json'
    Write-BytesAtomicallyExclusive `
        -Path $receiptDestination `
        -Bytes $receiptRead.bytes
    Write-BytesAtomicallyExclusive `
        -Path $indexDestination `
        -Bytes $indexRead.bytes
    $copiedReceipt = Read-TrustedEvidenceFileOnce `
        -Root $destinationRoot `
        -RelativePath 'authority-receipt.json' `
        -Label 'copied authority receipt'
    $copiedIndex = Read-TrustedEvidenceFileOnce `
        -Root $destinationRoot `
        -RelativePath 'artifact-index.json' `
        -Label 'copied artifact index'
    if ($copiedReceipt.sha256 -cne $traceReceiptHash -or
        $copiedIndex.sha256 -cne $traceIndexHash) {
        throw 'Copied trusted evidence receipt/index bytes differ from their validated source.'
    }
    return [pscustomobject] [ordered] @{
        artifactCount = [long] $artifactCount
        artifactIndex = [pscustomobject] [ordered] @{
            path = 'release/' + (Get-RelativeUnixPath `
                -Root $releaseFull `
                -Path $indexDestination)
            sha256 = $traceIndexHash
        }
        artifacts = @($copiedArtifacts)
        assertionCount = [long] $assertionCount
        authorityReceipt = [pscustomobject] [ordered] @{
            path = 'release/' + (Get-RelativeUnixPath `
                -Root $releaseFull `
                -Path $receiptDestination)
            sha256 = $traceReceiptHash
        }
        bundleRoot = 'release/' + (Get-RelativeUnixPath `
            -Root $releaseFull `
            -Path $destinationRoot)
        copiedArtifactCount = [long] ($artifactCount + 2)
        projectCount = [long] $projectCount
        sessionId = $sessionId
        targetFramework = $ExpectedTargetFramework
    }
}

function Get-TrustedEvidenceReportSession {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object] $EvidenceExecution
    )

    if ($null -eq $EvidenceExecution -or
        $EvidenceExecution -isnot [pscustomobject]) {
        throw 'Trusted evidence execution must be a JSON object.'
    }
    $hashes = $EvidenceExecution.result_artifact_sha256s
    $traces = $EvidenceExecution.result_artifacts
    $frameworks = $EvidenceExecution.target_frameworks
    if ($hashes -isnot [System.Array] -or
        $traces -isnot [System.Array] -or
        $frameworks -isnot [System.Array] -or
        @($hashes).Count -ne 1 -or
        @($traces).Count -ne 1 -or
        @($frameworks).Count -ne 1) {
        throw 'Trusted evidence report hashes, traces, and frameworks must be singleton JSON arrays.'
    }
    $hash = Assert-TrustedEvidenceCanonicalHash `
        -Value $hashes[0] `
        -Label 'evidence execution result artifact hash'
    if ($frameworks[0] -isnot [string] -or
        $frameworks[0] -cne 'net8.0-windows') {
        throw 'Trusted evidence report must bind target framework net8.0-windows.'
    }
    return [pscustomobject] [ordered] @{
        authorityReceiptSha256 = $hash
        targetFramework = [string] $frameworks[0]
        trace = $traces[0]
    }
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
    Assert-NoReparsePoints -Path $safeScratchRoot -AnchorPath $repositoryRoot

    if (Test-Path -LiteralPath $releaseRoot) {
        throw "Release staging directory already exists: '$releaseRoot'."
    }

    if (Test-Path -LiteralPath $finalReleaseRoot) {
        $safeExisting = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path $finalReleaseRoot `
            -AllowedTopLevelNames @('artifacts')
        Assert-NoReparseAncestorChain -Root $repositoryRoot -Candidate $safeExisting
        Assert-NoReparsePoints -Path $safeExisting -AnchorPath $repositoryRoot
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
    Assert-NoReparsePoints -Path $releaseRoot -AnchorPath $repositoryRoot
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
    Assert-NoReparsePoints -Path $safeStaging -AnchorPath $repositoryRoot
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
    -Path (Join-Path $repositoryRoot 'dev.cmd') `
    -Arguments @('setup', '-InstallEnergyPlus', '-RequireEnergyPlus', '-RequireRhino7', '-RequireRhino8') `
    -FailureMessage 'Release environment setup failed'

Write-Host 'Verifying the pinned Python compatibility oracle...'
Invoke-RepositoryCommand `
    -Path (Join-Path $repositoryRoot 'dev.cmd') `
    -Arguments @('reference', '-Mode', 'Verify') `
    -FailureMessage 'Python compatibility oracle failed'

Write-Host 'Running the exact 1,242-symbol trusted upstream compatibility gate...'
Invoke-RepositoryCommand `
    -Path (Join-Path $repositoryRoot 'dev.cmd') `
    -Arguments @(
        'upstream',
        'compatibility-gate',
        '--source-root', $upstreamRoot,
        '--output', $upstreamGatePath,
        '--collect-evidence') `
    -FailureMessage 'Exact upstream public-symbol compatibility gate failed'
$upstreamGateRead = Read-JsonBytesOnce -Path $upstreamGatePath
$upstreamCompatibility = $upstreamGateRead.json
if ($upstreamCompatibility.schema -isnot [string] -or
    $upstreamCompatibility.schema -cne 'goniegonie.upstream-compatibility-report.v2') {
    throw 'Exact upstream public-symbol compatibility report has the wrong schema.'
}

$requiredTrueProperties = [ordered] @{
    'gate.passed' = $upstreamCompatibility.gate.passed
    'gate.classification_complete' = $upstreamCompatibility.gate.classification_complete
    'gate.exact_inventory_coverage' = $upstreamCompatibility.gate.exact_inventory_coverage
    'gate.exact_registry_coverage' = $upstreamCompatibility.gate.exact_registry_coverage
    'gate.required_symbol_evidence_satisfied' = $upstreamCompatibility.gate.required_symbol_evidence_satisfied
    'gate.pinned_source_verified' = $upstreamCompatibility.gate.pinned_source_verified
    'gate.source_matches_inventory' = $upstreamCompatibility.gate.source_matches_inventory
    'gate.exception_bindings_match_source' = $upstreamCompatibility.gate.exception_bindings_match_source
    'gate.exact_test_source_bindings' = $upstreamCompatibility.gate.exact_test_source_bindings
    'gate.no_skipped_evidence' = $upstreamCompatibility.gate.no_skipped_evidence
    'gate.no_structural_only_evidence' = $upstreamCompatibility.gate.no_structural_only_evidence
    'gate.no_zero_load_active_overclaim' = $upstreamCompatibility.gate.no_zero_load_active_overclaim
    'evidence_execution.passed' = $upstreamCompatibility.evidence_execution.passed
    'evidence_execution.assertions_satisfied' = $upstreamCompatibility.evidence_execution.assertions_satisfied
    'evidence_execution.authoritative' = $upstreamCompatibility.evidence_execution.authoritative
}
foreach ($item in $requiredTrueProperties.GetEnumerator()) {
    Assert-JsonTrue -Value $item.Value -Label $item.Key
}

$publicSymbolCount = Get-JsonInteger `
    -Value $upstreamCompatibility.inventory.public_symbol_count `
    -Label 'inventory.public_symbol_count'
$matrixEntryCount = Get-JsonInteger `
    -Value $upstreamCompatibility.matrix.entry_count `
    -Label 'matrix.entry_count'
$requiredAssertionCount = Get-JsonInteger `
    -Value $upstreamCompatibility.evidence_execution.required_assertion_count `
    -Label 'evidence_execution.required_assertion_count'
$collectedAssertionCount = Get-JsonInteger `
    -Value $upstreamCompatibility.evidence_execution.collected_assertion_count `
    -Label 'evidence_execution.collected_assertion_count'
if ($publicSymbolCount -ne 1242 -or $matrixEntryCount -ne 1242) {
    throw 'Exact upstream compatibility inventory and matrix must both contain 1,242 symbols.'
}
if ($requiredAssertionCount -le 0 -or
    $requiredAssertionCount -ne $collectedAssertionCount) {
    throw 'Trusted upstream evidence must collect every required assertion and require at least one assertion.'
}

$classificationCounts = [ordered] @{
    equivalent = Get-JsonInteger `
        -Value $upstreamCompatibility.classification_counts.equivalent `
        -Label 'classification_counts.equivalent'
    exception = Get-JsonInteger `
        -Value $upstreamCompatibility.classification_counts.exception `
        -Label 'classification_counts.exception'
    out_of_scope = Get-JsonInteger `
        -Value $upstreamCompatibility.classification_counts.out_of_scope `
        -Label 'classification_counts.out_of_scope'
    needs_reverification = Get-JsonInteger `
        -Value $upstreamCompatibility.classification_counts.needs_reverification `
        -Label 'classification_counts.needs_reverification'
}
$classificationTotal = [long] 0
foreach ($count in $classificationCounts.Values) {
    if ($count -lt 0) {
        throw 'Exact upstream compatibility classification counts cannot be negative.'
    }
    $classificationTotal += $count
}
if ($classificationTotal -ne 1242 -or $classificationCounts.needs_reverification -ne 0) {
    throw 'Exact upstream compatibility classifications must total 1,242 with none needing reverification.'
}

$requiredEmptyArrays = [ordered] @{
    unresolved = $upstreamCompatibility.unresolved
    'evidence_execution.missing_assertion_ids' = $upstreamCompatibility.evidence_execution.missing_assertion_ids
    'evidence_execution.failed_assertion_ids' = $upstreamCompatibility.evidence_execution.failed_assertion_ids
    'evidence_execution.skipped_assertion_ids' = $upstreamCompatibility.evidence_execution.skipped_assertion_ids
    'evidence_execution.structural_only_assertion_ids' = $upstreamCompatibility.evidence_execution.structural_only_assertion_ids
    'evidence_execution.output_hash_mismatch_ids' = $upstreamCompatibility.evidence_execution.output_hash_mismatch_ids
    'evidence_execution.load_mismatch_ids' = $upstreamCompatibility.evidence_execution.load_mismatch_ids
    'evidence_execution.test_binding_mismatch_ids' = $upstreamCompatibility.evidence_execution.test_binding_mismatch_ids
}
foreach ($item in $requiredEmptyArrays.GetEnumerator()) {
    Assert-JsonEmptyArray -Value $item.Value -Label $item.Key
}

$requiredAssertionIds = $upstreamCompatibility.evidence_execution.required_assertion_ids
$collectedAssertionIds = $upstreamCompatibility.evidence_execution.collected_assertion_ids
$resultArtifactHashes = $upstreamCompatibility.evidence_execution.result_artifact_sha256s
if ($requiredAssertionIds -isnot [System.Array] -or
    @($requiredAssertionIds).Count -ne $requiredAssertionCount -or
    $collectedAssertionIds -isnot [System.Array] -or
    @($collectedAssertionIds).Count -ne $collectedAssertionCount -or
    $resultArtifactHashes -isnot [System.Array] -or
    @($resultArtifactHashes).Count -le 0) {
    throw 'Trusted upstream evidence identifier and artifact arrays do not match their declared counts.'
}
for ($index = 0; $index -lt $requiredAssertionIds.Count; $index += 1) {
    if ($requiredAssertionIds[$index] -isnot [string] -or
        [string]::IsNullOrWhiteSpace($requiredAssertionIds[$index]) -or
        $collectedAssertionIds[$index] -isnot [string] -or
        $requiredAssertionIds[$index] -cne $collectedAssertionIds[$index]) {
        throw 'Trusted upstream required and collected assertion identifiers must be identical ordered string arrays.'
    }
    if ($index -gt 0 -and
        [string]::CompareOrdinal($requiredAssertionIds[$index - 1], $requiredAssertionIds[$index]) -ge 0) {
        throw 'Trusted upstream assertion identifiers must be unique and ordinally sorted.'
    }
}
foreach ($artifactHash in $resultArtifactHashes) {
    if ($artifactHash -isnot [string] -or $artifactHash -cnotmatch '^sha256:[0-9a-f]{64}$') {
        throw 'Trusted upstream evidence artifact hashes must be canonical SHA-256 values.'
    }
}

$trustedEvidenceReportSession = Get-TrustedEvidenceReportSession `
    -EvidenceExecution $upstreamCompatibility.evidence_execution
Ensure-Directory -Path $trustedEvidenceReleaseRoot
Assert-NoReparseAncestorChain -Root $repositoryRoot -Candidate $trustedEvidenceReleaseRoot
$trustedEvidenceAttestation = Copy-TrustedEvidenceSession `
    -RepositoryRoot $repositoryRoot `
    -ReleaseRoot $releaseRoot `
    -TrustedEvidenceReleaseRoot $trustedEvidenceReleaseRoot `
    -Trace $trustedEvidenceReportSession.trace `
    -ExpectedAuthorityReceiptSha256 $trustedEvidenceReportSession.authorityReceiptSha256 `
    -ExpectedRepositoryHead $commit `
    -ExpectedUpstreamCommit ([string] $upstreamCompatibility.pinned.commit) `
    -ExpectedInventorySha256 ([string] $upstreamCompatibility.inventory.content_sha256) `
    -ExpectedMatrixSha256 ([string] $upstreamCompatibility.matrix.content_sha256) `
    -ExpectedSymbolEvidenceSha256 ([string] $upstreamCompatibility.symbol_evidence.content_sha256) `
    -ExpectedTargetFramework $trustedEvidenceReportSession.targetFramework `
    -ExpectedAssertionCount $requiredAssertionCount `
    -ExpectedAssertionIds $requiredAssertionIds
$trustedEvidenceAttestations = @($trustedEvidenceAttestation)
if ($trustedEvidenceAttestation.assertionCount -ne $requiredAssertionCount -or
    $trustedEvidenceAttestation.projectCount -le 0 -or
    $trustedEvidenceAttestation.artifactCount -le 0 -or
    $trustedEvidenceAttestation.copiedArtifactCount -ne
        ($trustedEvidenceAttestation.artifactCount + 2)) {
    throw 'Trusted evidence bundle attestation counts do not reconcile with the report.'
}

Assert-NoReparseAncestorChain -Root $repositoryRoot -Candidate $upstreamReleasePath
Write-BytesAtomicallyExclusive `
    -Path $upstreamReleasePath `
    -Bytes $upstreamGateRead.bytes
$upstreamGateCopiedSha256 = Get-Sha256 -Path $upstreamReleasePath
if ($upstreamGateCopiedSha256 -cne $upstreamGateRead.sha256) {
    throw 'The copied upstream compatibility report differs from the exact bytes that passed validation.'
}

Write-Host 'Building and testing all Rhino targets with EnergyPlus integration...'
Invoke-RepositoryCommand `
    -Path (Join-Path $repositoryRoot 'dev.cmd') `
    -Arguments @('build', '-NoRestore', '-RequireEnergyPlus') `
    -FailureMessage 'Release build failed'

Write-Host 'Running strict cross-language engineering compatibility cases...'
Invoke-RepositoryCommand `
    -Path (Join-Path $repositoryRoot 'dev.cmd') `
    -Arguments @('compatibility', '-SkipReferencePreparation', '-NoRestore') `
    -FailureMessage 'Engineering compatibility gate failed'

Write-Host 'Opening and round-trip validating the tracked examples in Rhino 7 and Rhino 8...'
Invoke-RepositoryCommand `
    -Path (Join-Path $repositoryRoot 'dev.cmd') `
    -Arguments @(
        'examples',
        '-SkipPluginBuild',
        '-RequireEnergyPlusWorkflow',
        '-TimeoutSeconds', '900',
        '-WorkflowStageTimeoutSeconds', '240') `
    -FailureMessage 'Verified Grasshopper example gate failed'

Write-Host 'Packaging and loading the exact portable ZIPs in six fresh Rhino hosts...'
$hostRunsBeforePackage = @(Get-PortableHostGateRunPaths)
Invoke-RepositoryCommand `
    -Path (Join-Path $repositoryRoot 'dev.cmd') `
    -Arguments @('package', '-SkipBuild', '-RunPortableHostGate') `
    -FailureMessage 'Release packaging or portable host verification failed'

Assert-ReleaseSourceClean -Stage 'post-verification'

$settings = Require-Json `
    -Path $settingsPath `
    -Schema 'goniegonie.dragons-grasshopper.local-settings.v1'
$buildManifestPath = Join-Path $reportsRoot 'build-manifest.json'
$testSummaryPath = Join-Path $reportsRoot 'test-summary.json'
$engineeringCompatibilityPath = Join-Path $reportsRoot 'engineering-compatibility.json'
$engineeringReleasePath = Join-Path $releaseRoot 'engineering-compatibility.json'
$compatibilityExceptionsPath = Join-Path $repositoryRoot 'upstream\compatibility-exceptions.yml'
$packageIndexPath = Join-Path $packagesRoot 'package-index.json'
$compatibilityPath = Join-Path $packagesRoot 'compatibility-report.json'
$packageChecksumsPath = Join-Path $packagesRoot 'checksums.sha256'
$buildManifest = Require-Json `
    -Path $buildManifestPath `
    -Schema 'goniegonie.dragons-grasshopper.build-manifest.v1'
$testSummary = Require-Json `
    -Path $testSummaryPath `
    -Schema 'goniegonie.dragons-grasshopper.test-summary.v1'
$engineeringCompatibility = Require-Json `
    -Path $engineeringCompatibilityPath `
    -Schema 'goniegonie.dragons.engineering-compatibility-report.v1'
Assert-EngineeringPortProvenance `
    -Report $engineeringCompatibility `
    -ExpectedCommit $commit
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
$engineeringCases = @($engineeringCompatibility.cases)
$requiredEngineeringCaseIds = @(
    'ashrae-140-modified',
    'two-zone-one-sided-adjacency-shared-hp',
    'screw-chiller-closed-two-speed-fcu',
    'packaged-erv-pv-openings',
    'packaged-erv-pv-openings--tampa',
    'packaged-erv-pv-openings--golden',
    'packaged-erv-pv-openings--san-francisco',
    'geothermal-heat-pump-ahu',
    'boiler-heating-fuel-shared-matrix',
    'absorption-default-explicit-electric-radiant',
    'district-shared-fcu-radiator-radiant-dhw'
) | Sort-Object
$engineeringCaseIds = @($engineeringCases | ForEach-Object { [string] $_.id } | Sort-Object)
$engineeringStageReceiptCount = [int] ($engineeringCases | ForEach-Object {
    @($_.executed_stages).Count
} | Measure-Object -Sum).Sum
if (-not [bool] $engineeringCompatibility.passed -or
    [int] $engineeringCompatibility.declared_case_count -ne 11 -or
    [int] $engineeringCompatibility.executed_case_count -ne [int] $engineeringCompatibility.declared_case_count -or
    [int] $engineeringCompatibility.passed_case_count -ne [int] $engineeringCompatibility.declared_case_count -or
    [int] $engineeringCompatibility.failed_case_count -ne 0 -or
    [int] $engineeringCompatibility.skip_count -ne 0 -or
    $engineeringCases.Count -ne 11 -or
    $engineeringStageReceiptCount -ne 66 -or
    @($engineeringCaseIds | Select-Object -Unique).Count -ne 11 -or
    @(Compare-Object -ReferenceObject $requiredEngineeringCaseIds -DifferenceObject $engineeringCaseIds).Count -ne 0 -or
    @($engineeringCases | Where-Object {
        -not [bool] $_.passed -or
        [int] $_.skip_count -ne 0 -or
        @($_.skipped_stages).Count -ne 0
    }).Count -ne 0) {
    throw 'Engineering compatibility report is incomplete, skipped, or failed.'
}
$requiredEngineeringStages = @(
    'grm_cross_read',
    'authoring_idf',
    'expanded_idf',
    'energyplus',
    'grr',
    'warnings'
)
$limitedEngineeringCases = @()
$diagnosticEngineeringExceptions = @()
foreach ($engineeringCase in $engineeringCases) {
    $caseId = [string] $engineeringCase.id
    $declaredStages = @($engineeringCase.declared_stages | ForEach-Object { [string] $_ } | Sort-Object)
    $executedStages = @($engineeringCase.executed_stages | ForEach-Object { [string] $_ } | Sort-Object)
    if (@(Compare-Object `
            -ReferenceObject @($requiredEngineeringStages | Sort-Object) `
            -DifferenceObject $declaredStages).Count -ne 0 -or
        @(Compare-Object `
            -ReferenceObject @($requiredEngineeringStages | Sort-Object) `
            -DifferenceObject $executedStages).Count -ne 0) {
        throw "Engineering case '$caseId' did not declare and execute the exact six release stages."
    }

    $stageScope = $engineeringCase.stage_scope
    if ($null -ne $stageScope) {
        if (@($stageScope.excluded_stages).Count -ne 0) {
            throw "Engineering case '$caseId' excludes a release stage."
        }
        $notVerified = @($stageScope.not_verified | Where-Object {
            -not [string]::IsNullOrWhiteSpace([string] $_)
        })
        if ($notVerified.Count -ne 0) {
            if ([string]::IsNullOrWhiteSpace([string] $stageScope.exception_id) -or
                [string]::IsNullOrWhiteSpace([string] $stageScope.diagnostic) -or
                @($stageScope.verified).Count -eq 0) {
                throw "Engineering case '$caseId' has an incomplete not_verified exception policy."
            }
            $limitedEngineeringCases += $engineeringCase
        }
    }

    foreach ($diagnosticException in @($engineeringCase.diagnostic_exceptions)) {
        $severity = ([string] $diagnosticException.severity).ToLowerInvariant()
        if ([string]::IsNullOrWhiteSpace([string] $diagnosticException.exception_id) -or
            [string]::IsNullOrWhiteSpace([string] $diagnosticException.title) -or
            $severity -notin @('severe', 'fatal') -or
            [int] $diagnosticException.count -lt 1) {
            throw "Engineering case '$caseId' has an invalid diagnostic exception policy."
        }
        $diagnosticEngineeringExceptions += $diagnosticException
    }
}
$expectedLimitationIds = @($limitedEngineeringCases |
    ForEach-Object { [string] $_.stage_scope.exception_id } |
    Sort-Object -Unique)
$actualLimitationIds = @($engineeringCompatibility.limitation_exception_ids |
    ForEach-Object { [string] $_ } |
    Sort-Object -Unique)
$expectedDiagnosticIds = @($diagnosticEngineeringExceptions |
    ForEach-Object { [string] $_.exception_id } |
    Sort-Object -Unique)
$actualDiagnosticIds = @($engineeringCompatibility.diagnostic_exception_ids |
    ForEach-Object { [string] $_ } |
    Sort-Object -Unique)
$expectedReferencedIds = @(($expectedLimitationIds + $expectedDiagnosticIds) | Sort-Object -Unique)
$actualReferencedIds = @($engineeringCompatibility.referenced_exception_ids |
    ForEach-Object { [string] $_ } |
    Sort-Object -Unique)
if ([int] $engineeringCompatibility.limitation_count -ne $limitedEngineeringCases.Count -or
    [int] $engineeringCompatibility.diagnostic_exception_count -ne $diagnosticEngineeringExceptions.Count -or
    @(Compare-Object -ReferenceObject $expectedLimitationIds -DifferenceObject $actualLimitationIds).Count -ne 0 -or
    @(Compare-Object -ReferenceObject $expectedDiagnosticIds -DifferenceObject $actualDiagnosticIds).Count -ne 0 -or
    @(Compare-Object -ReferenceObject $expectedReferencedIds -DifferenceObject $actualReferencedIds).Count -ne 0) {
    throw 'Engineering compatibility limitation/diagnostic exception summaries are inconsistent.'
}
if (-not (Test-Path -LiteralPath $compatibilityExceptionsPath -PathType Leaf) -or
    -not ([string] $engineeringCompatibility.exception_registry_sha256).Equals(
        (Get-Sha256 -Path $compatibilityExceptionsPath),
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Engineering compatibility report does not attest the current exception registry.'
}
Copy-Item `
    -LiteralPath $engineeringCompatibilityPath `
    -Destination $engineeringReleasePath `
    -Force
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
    Get-Item -LiteralPath $engineeringCompatibilityPath
)
if (@($releaseAssets | Where-Object { $_.Extension -eq '.yak' }).Count -ne 4 -or
    @($releaseAssets | Where-Object { $_.Extension -eq '.zip' }).Count -ne 2 -or
    $releaseAssets.Count -ne 10) {
    throw "Expected four Yak archives, two portable ZIPs, and four common reports; found $($releaseAssets.Count) release assets."
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
        upstreamPublicSymbolCompatibility = [pscustomobject] [ordered] @{
            status = 'passed'
            publicSymbolCount = [int] $publicSymbolCount
            requiredAssertionCount = [int] $requiredAssertionCount
            trustedEvidenceProjectCount = [int] $trustedEvidenceAttestation.projectCount
            trustedEvidenceArtifactCount = [int] $trustedEvidenceAttestation.artifactCount
            trustedEvidenceCopiedArtifactCount = [int] $trustedEvidenceAttestation.copiedArtifactCount
            report = 'release/' + (Get-RelativeUnixPath `
                -Root $releaseRoot `
                -Path $upstreamReleasePath)
            sha256 = $upstreamGateCopiedSha256
            trustedEvidence = @($trustedEvidenceAttestations)
        }
        managedAndIntegrationTests = [string] $testSummary.status
        engineeringCompatibility = [pscustomobject] [ordered] @{
            status = 'passed'
            declaredCaseCount = [int] $engineeringCompatibility.declared_case_count
            executedCaseCount = [int] $engineeringCompatibility.executed_case_count
            skippedStageCount = [int] $engineeringCompatibility.skip_count
            limitationCount = [int] $engineeringCompatibility.limitation_count
            diagnosticExceptionCount = [int] $engineeringCompatibility.diagnostic_exception_count
            referencedExceptionIds = @($engineeringCompatibility.referenced_exception_ids)
            report = 'release/' + (Get-RelativeUnixPath `
                -Root $releaseRoot `
                -Path $engineeringReleasePath)
            sha256 = Get-Sha256 -Path $engineeringReleasePath
        }
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
    Get-Item -LiteralPath $engineeringReleasePath
    Get-Item -LiteralPath $upstreamReleasePath
    Get-ChildItem -LiteralPath $trustedEvidenceReleaseRoot -File -Recurse
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

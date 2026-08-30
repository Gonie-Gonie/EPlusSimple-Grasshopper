#requires -Version 5.1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $repositoryRoot 'scripts\install-rhino-host.ps1')

function Assert-Equal {
    param(
        [AllowNull()]
        [object] $Expected,

        [AllowNull()]
        [object] $Actual,

        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected'; found '$Actual'."
    }
}

function Assert-ThrowsLike {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock] $Action,

        [Parameter(Mandatory = $true)]
        [string] $Pattern
    )

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -notlike $Pattern) {
            throw "Expected error like '$Pattern'; found '$($_.Exception.Message)'."
        }
        return
    }

    throw "Expected action to fail with an error like '$Pattern'."
}

function Assert-NoHostVariableWrites {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile(
        $Path,
        [ref] $tokens,
        [ref] $parseErrors)
    if ($parseErrors.Count -ne 0) {
        throw "Installer script could not be parsed: $($parseErrors[0].Message)"
    }

    $writes = @($ast.FindAll({
        param($node)

        if ($node -is [System.Management.Automation.Language.AssignmentStatementAst]) {
            return $node.Left -is [System.Management.Automation.Language.VariableExpressionAst] -and
                $node.Left.VariablePath.UserPath -ieq 'Host'
        }
        if ($node -is [System.Management.Automation.Language.ForEachStatementAst]) {
            return $node.Variable.VariablePath.UserPath -ieq 'Host'
        }
        if ($node -is [System.Management.Automation.Language.ParameterAst]) {
            return $node.Name.VariablePath.UserPath -ieq 'Host'
        }

        return $false
    }, $true))
    Assert-Equal -Expected 0 -Actual $writes.Count `
        -Message 'Installer must not write PowerShell automatic read-only variable $Host.'
}

Assert-NoHostVariableWrites -Path (Join-Path $repositoryRoot 'scripts\install.ps1')

$testRoot = Join-Path $repositoryRoot (
    Join-Path 'temp\installer-tests' ([Guid]::NewGuid().ToString('N')))
$tempParent = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'temp'))
$resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
$tempPrefix = $tempParent.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedTestRoot.StartsWith(
        $tempPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Installer test root escaped the repository temp directory: '$resolvedTestRoot'."
}

try {
    $customRoot = Join-Path $resolvedTestRoot 'Custom Rhino 8'
    $customSystem = Join-Path $customRoot 'System'
    $customExecutable = Join-Path $customSystem 'Rhino.exe'
    $customYak = Join-Path $customSystem 'yak.exe'
    $standardExecutable = Join-Path $resolvedTestRoot 'Standard\System\Rhino.exe'

    Assert-Equal `
        -Expected $customExecutable `
        -Actual (ConvertTo-RhinoExecutablePath -Path $customRoot) `
        -Message 'A Rhino root must resolve through its System directory.'
    Assert-Equal `
        -Expected $customExecutable `
        -Actual (ConvertTo-RhinoExecutablePath -Path $customSystem) `
        -Message 'A Rhino System directory must resolve directly to Rhino.exe.'
    Assert-Equal `
        -Expected $customExecutable `
        -Actual (ConvertTo-RhinoExecutablePath -Path $customExecutable -PathKind Executable) `
        -Message 'An exact Rhino.exe path must remain unchanged.'
    Assert-ThrowsLike `
        -Action { ConvertTo-RhinoExecutablePath -Path $customYak -PathKind Executable } `
        -Pattern '*must be named Rhino.exe*'

    $settings = [pscustomobject] [ordered] @{
        schema = 'goniegonie.dragons-grasshopper.local-settings.v1'
        rhino = [pscustomobject] [ordered] @{
            rhino7 = [pscustomobject] [ordered] @{
                status = 'missing'
                executable = $null
                root = $null
            }
            rhino8 = [pscustomobject] [ordered] @{
                status = 'ready'
                executable = $customExecutable
                root = $customRoot
            }
        }
    }

    $candidate = Resolve-RhinoHostCandidate `
        -Name Rhino8 `
        -MajorVersion 8 `
        -PackageTarget rhino8 `
        -ExplicitPath $null `
        -LocalSettings $settings `
        -StandardExecutable $standardExecutable
    Assert-Equal -Expected 'local-settings' -Actual $candidate.source `
        -Message 'Ready local settings must take precedence over the standard location.'
    Assert-Equal -Expected $customExecutable -Actual $candidate.rhino `
        -Message 'The configured executable must be selected.'

    $explicitRoot = Join-Path $resolvedTestRoot 'Explicit Rhino 8'
    $explicitExecutable = Join-Path $explicitRoot 'System\Rhino.exe'
    $candidate = Resolve-RhinoHostCandidate `
        -Name Rhino8 `
        -MajorVersion 8 `
        -PackageTarget rhino8 `
        -ExplicitPath $explicitRoot `
        -LocalSettings $settings `
        -StandardExecutable $standardExecutable
    Assert-Equal -Expected 'install-argument' -Actual $candidate.source `
        -Message 'An explicit install argument must override persisted settings.'
    Assert-Equal -Expected $explicitExecutable -Actual $candidate.rhino `
        -Message 'An explicit Rhino root must normalize to Rhino.exe.'

    $candidate = Resolve-RhinoHostCandidate `
        -Name Rhino7 `
        -MajorVersion 7 `
        -PackageTarget rhino7 `
        -ExplicitPath $null `
        -LocalSettings $settings `
        -StandardExecutable $standardExecutable
    Assert-Equal -Expected 'standard-location' -Actual $candidate.source `
        -Message 'An absent configured path must fall back to the standard location.'
    Assert-Equal -Expected $standardExecutable -Actual $candidate.rhino `
        -Message 'The standard executable must be preserved exactly.'

    $settings.rhino.rhino8.status = 'incompatible'
    Assert-ThrowsLike -Action {
        Resolve-RhinoHostCandidate `
            -Name Rhino8 `
            -MajorVersion 8 `
            -PackageTarget rhino8 `
            -ExplicitPath $null `
            -LocalSettings $settings `
            -StandardExecutable $standardExecutable
    } -Pattern "*status is 'incompatible'*"
    $settings.rhino.rhino8.status = 'ready'
    $settings.rhino.rhino8.root = Join-Path $resolvedTestRoot 'Different Rhino 8'
    Assert-ThrowsLike -Action {
        Resolve-RhinoHostCandidate `
            -Name Rhino8 `
            -MajorVersion 8 `
            -PackageTarget rhino8 `
            -ExplicitPath $null `
            -LocalSettings $settings `
            -StandardExecutable $standardExecutable
    } -Pattern '*resolve to different installations*'
    $settings.rhino.rhino8.root = $customRoot

    New-Item -ItemType Directory -Path $customSystem -Force | Out-Null
    [System.IO.File]::WriteAllBytes($customExecutable, [byte[]] @(77, 90))
    Assert-ThrowsLike `
        -Action { Get-RhinoExecutablePair -Name Rhino8 -Executable $customExecutable } `
        -Pattern '*sibling yak.exe is missing*'
    [System.IO.File]::WriteAllBytes($customYak, [byte[]] @(77, 90))
    $pair = Get-RhinoExecutablePair -Name Rhino8 -Executable $customExecutable
    Assert-Equal -Expected $customExecutable -Actual $pair.rhino `
        -Message 'The validated pair must retain Rhino.exe.'
    Assert-Equal -Expected $customYak -Actual $pair.yak `
        -Message 'The validated pair must use only sibling yak.exe.'

    $rhino7Executable = Join-Path $resolvedTestRoot 'Rhino 7\System\Rhino.exe'
    $setupArguments = @(Get-RhinoSetupArguments -Hosts @(
        [pscustomobject] @{ name = 'Rhino8'; rhino = $customExecutable },
        [pscustomobject] @{ name = 'Rhino7'; rhino = $rhino7Executable }
    ))
    Assert-Equal -Expected 6 -Actual $setupArguments.Count `
        -Message 'Setup forwarding must produce three arguments for each selected host.'
    Assert-Equal -Expected '-Rhino7Path' -Actual $setupArguments[0] `
        -Message 'Rhino 7 setup path switch is missing.'
    Assert-Equal -Expected $rhino7Executable -Actual $setupArguments[1] `
        -Message 'Rhino 7 setup path was not preserved.'
    Assert-Equal -Expected '-RequireRhino7' -Actual $setupArguments[2] `
        -Message 'Rhino 7 requirement switch is missing.'
    Assert-Equal -Expected '-Rhino8Path' -Actual $setupArguments[3] `
        -Message 'Rhino 8 setup path switch is missing.'
    Assert-Equal -Expected $customExecutable -Actual $setupArguments[4] `
        -Message 'Rhino 8 setup path was not preserved.'
    Assert-Equal -Expected '-RequireRhino8' -Actual $setupArguments[5] `
        -Message 'Rhino 8 requirement switch is missing.'
}
finally {
    if (Test-Path -LiteralPath $resolvedTestRoot) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}

Write-Host 'Installer Rhino path tests passed.'

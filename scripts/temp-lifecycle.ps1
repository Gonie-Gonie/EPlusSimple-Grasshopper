# Shared lifecycle policy for disposable repository-local run directories.
# Keep this file compatible with Windows PowerShell 5.1.

Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

$script:TempWorkflowEnvironmentVariable = 'DRAGONS_TEMP_WORKFLOW'

function Test-ForwardedWhatIfRequest {
    [CmdletBinding()]
    param(
        [AllowEmptyCollection()]
        [string[]] $Arguments = @()
    )

    foreach ($argument in @($Arguments)) {
        # Windows PowerShell accepts unique common-parameter prefixes such as
        # -Wh, its built-in -Wi shorthand, and colon forms such as
        # -WhatIf:$true. An explicit false value is a mutating request and
        # therefore must still acquire the temp lease.
        if ([string]::IsNullOrWhiteSpace($argument)) {
            continue
        }
        $match = [regex]::Match(
            $argument.Trim(),
            '^-(?:wi|wh(?:a(?:t(?:i(?:f)?)?)?)?)(?::(?<value>.*))?$',
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if (-not $match.Success) {
            continue
        }
        if (-not $match.Groups['value'].Success -or
            $match.Groups['value'].Value -imatch '^\$?(?:true|1)$') {
            return $true
        }
    }
    return $false
}

function Get-RepositoryTempWorkflowLockPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    $root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    $lockDirectory = Assert-RepositoryChildPath `
        -RepositoryRoot $root `
        -Path (Join-Path $root '.tools\temp-workflow') `
        -AllowedTopLevelNames @('.tools')
    Assert-NoReparsePoints -Path $lockDirectory -AnchorPath $root
    Ensure-Directory -Path $lockDirectory
    Assert-NoReparsePoints -Path $lockDirectory -AnchorPath $root
    return Join-Path $lockDirectory 'workflow.lock'
}

function Enter-RepositoryTempWorkflow {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    $lockPath = Get-RepositoryTempWorkflowLockPath -RepositoryRoot $RepositoryRoot
    Assert-NoReparsePoints -Path $lockPath -AnchorPath $RepositoryRoot
    try {
        return New-Object System.IO.FileStream(
            $lockPath,
            [System.IO.FileMode]::OpenOrCreate,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
    }
    catch [System.IO.IOException] {
        throw (
            'Another dev.cmd workflow is already using repository temp. ' +
            'Wait for it to finish before starting a second workflow.')
    }
}

function Test-RepositoryTempWorkflowHeld {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    $lockPath = Get-RepositoryTempWorkflowLockPath -RepositoryRoot $RepositoryRoot
    $probe = $null
    try {
        $probe = New-Object System.IO.FileStream(
            $lockPath,
            [System.IO.FileMode]::OpenOrCreate,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
        return $false
    }
    catch [System.IO.IOException] {
        return $true
    }
    finally {
        if ($null -ne $probe) {
            $probe.Dispose()
        }
    }
}

function Test-NestedRepositoryTempWorkflow {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    $inheritedWorkflow = [Environment]::GetEnvironmentVariable(
        $script:TempWorkflowEnvironmentVariable,
        [EnvironmentVariableTarget]::Process)
    return -not [string]::IsNullOrWhiteSpace($inheritedWorkflow) -and
        (Test-RepositoryTempWorkflowHeld -RepositoryRoot $RepositoryRoot)
}

function Start-RepositoryTempWorkflowContext {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [switch] $Preview
    )

    $previousEnvironmentValue = [Environment]::GetEnvironmentVariable(
        $script:TempWorkflowEnvironmentVariable,
        [EnvironmentVariableTarget]::Process)
    if ($Preview) {
        return [pscustomobject] [ordered] @{
            mode = 'preview'
            lease = $null
            previousEnvironmentValue = $previousEnvironmentValue
        }
    }

    if (Test-NestedRepositoryTempWorkflow -RepositoryRoot $RepositoryRoot) {
        return [pscustomobject] [ordered] @{
            mode = 'nested'
            lease = $null
            previousEnvironmentValue = $previousEnvironmentValue
        }
    }

    $lease = Enter-RepositoryTempWorkflow -RepositoryRoot $RepositoryRoot
    try {
        [Environment]::SetEnvironmentVariable(
            $script:TempWorkflowEnvironmentVariable,
            [Guid]::NewGuid().ToString('N'),
            [EnvironmentVariableTarget]::Process)
    }
    catch {
        $lease.Dispose()
        throw
    }

    return [pscustomobject] [ordered] @{
        mode = 'owner'
        lease = $lease
        previousEnvironmentValue = $previousEnvironmentValue
    }
}

function Stop-RepositoryTempWorkflowContext {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object] $Context
    )

    if ([string] $Context.mode -ne 'owner') {
        return
    }

    try {
        [Environment]::SetEnvironmentVariable(
            $script:TempWorkflowEnvironmentVariable,
            $Context.previousEnvironmentValue,
            [EnvironmentVariableTarget]::Process)
    }
    finally {
        if ($null -ne $Context.lease) {
            $Context.lease.Dispose()
        }
    }
}

function Get-RepositoryTempRunPolicies {
    [CmdletBinding()]
    param()

    return @(
        [pscustomobject] [ordered] @{
            relativeRoot = 'install'
            namePattern = '^run-[0-9]{8}-[0-9]{6}-[0-9]{3}$'
            retentionMode = 'receipt'
        },
        [pscustomobject] [ordered] @{
            relativeRoot = 'grasshopper-smoke'
            namePattern = '^run-[0-9]{8}-[0-9]{6}-[0-9]{3}$'
            retentionMode = 'heavy-run'
        },
        [pscustomobject] [ordered] @{
            relativeRoot = 'e'
            namePattern = '^[0-9a-f]{8}$'
            retentionMode = 'heavy-run'
        },
        [pscustomobject] [ordered] @{
            relativeRoot = 'u'
            namePattern = '^[0-9a-f]{32}$'
            retentionMode = 'heavy-run'
        },
        [pscustomobject] [ordered] @{
            relativeRoot = 'release-candidate'
            namePattern = '^staging-[0-9]{8}-[0-9]{6}-[0-9]{3}$'
            retentionMode = 'heavy-run'
        },
        [pscustomobject] [ordered] @{
            relativeRoot = 'release-candidate'
            namePattern = '^previous-[0-9]{8}-[0-9]{6}-[0-9]{3}$'
            retentionMode = 'release-recovery'
        },
        [pscustomobject] [ordered] @{
            relativeRoot = 'installer-tests'
            namePattern = '^[0-9a-f]{32}$'
            retentionMode = 'heavy-run'
        }
    )
}

function Invoke-RepositoryTempRunRetention {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [ValidateRange(1, 20)]
        [int] $KeepLatest = 1,

        [ValidateSet('Before', 'Success', 'Failure')]
        [string] $Phase = 'Before',

        [AllowEmptyCollection()]
        [string[]] $ProtectedPaths = @()
    )

    $root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    $tempRoot = Assert-RepositoryChildPath `
        -RepositoryRoot $root `
        -Path (Join-Path $root 'temp') `
        -AllowedTopLevelNames @('temp')

    $protectedFullPaths = @($ProtectedPaths | ForEach-Object {
        Assert-RepositoryChildPath `
            -RepositoryRoot $root `
            -Path $_ `
            -AllowedTopLevelNames @('temp')
    })

    $removed = New-Object System.Collections.ArrayList
    $retained = New-Object System.Collections.ArrayList
    $unremoved = New-Object System.Collections.ArrayList
    $warnings = New-Object System.Collections.ArrayList
    if (-not (Test-Path -LiteralPath $tempRoot -PathType Container)) {
        return [pscustomobject] [ordered] @{
            removed = @()
            retained = @()
            unremoved = @()
            warnings = @()
        }
    }

    foreach ($policy in @(Get-RepositoryTempRunPolicies)) {
        $collectionRoot = Assert-RepositoryChildPath `
            -RepositoryRoot $root `
            -Path (Join-Path $tempRoot ([string] $policy.relativeRoot)) `
            -AllowedTopLevelNames @('temp')
        if (-not (Test-Path -LiteralPath $collectionRoot -PathType Container)) {
            continue
        }

        $candidates = @(Get-ChildItem -LiteralPath $collectionRoot -Directory -Force |
            Where-Object {
                $_.Name -cmatch ([string] $policy.namePattern)
            } |
            Sort-Object `
                @{ Expression = { $_.LastWriteTimeUtc }; Descending = $true }, `
                @{ Expression = { $_.Name }; Descending = $true })

        $policyKeepLatest = $KeepLatest
        if ([string] $policy.retentionMode -eq 'heavy-run' -and
            $Phase -ne 'Failure') {
            # Successful heavy runs have durable outputs elsewhere. A failed
            # run is kept only until the next explicit top-level workflow.
            $policyKeepLatest = 0
        }
        elseif ([string] $policy.retentionMode -eq 'release-recovery') {
            $finalReleaseRoot = Assert-RepositoryChildPath `
                -RepositoryRoot $root `
                -Path (Join-Path $root 'artifacts\release') `
                -AllowedTopLevelNames @('artifacts')
            # A previous release is rollback material only while a failed gate
            # has left no current release. Once a current release exists, every
            # previous-* copy is superseded and can be removed.
            $policyKeepLatest = if (Test-Path -LiteralPath $finalReleaseRoot -PathType Container) {
                0
            }
            else {
                1
            }
        }

        $quotaIndex = 0
        foreach ($candidate in $candidates) {
            $safeCandidate = Assert-RepositoryChildPath `
                -RepositoryRoot $root `
                -Path $candidate.FullName `
                -AllowedTopLevelNames @('temp')
            if ($protectedFullPaths -contains $safeCandidate) {
                # A candidate that could not be removed before this workflow
                # must not consume the failed run's diagnostic quota.
                $null = $retained.Add($safeCandidate)
                continue
            }

            $withinQuota = $quotaIndex -lt $policyKeepLatest
            $quotaIndex += 1
            if ($withinQuota) {
                $null = $retained.Add($safeCandidate)
                continue
            }

            if (-not $PSCmdlet.ShouldProcess(
                    $safeCandidate,
                    'Remove superseded repository temp run')) {
                continue
            }

            try {
                Assert-NoReparsePoints -Path $safeCandidate -AnchorPath $root
                Remove-Item -LiteralPath $safeCandidate -Recurse -Force -ErrorAction Stop
                $null = $removed.Add($safeCandidate)
            }
            catch {
                # Automatic retention must not hide a developer's primary build
                # result. Leave an unsafe or locked directory untouched; the
                # explicit clean command remains strict and reports failures.
                $message = "Retained '$safeCandidate': $($_.Exception.Message)"
                $null = $unremoved.Add($safeCandidate)
                $null = $warnings.Add($message)
                Write-Warning $message
            }
        }
    }

    return [pscustomobject] [ordered] @{
        removed = @($removed)
        retained = @($retained)
        unremoved = @($unremoved)
        warnings = @($warnings)
    }
}

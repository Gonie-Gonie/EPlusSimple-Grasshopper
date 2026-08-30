#requires -Version 5.1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $repositoryRoot 'scripts\temp-lifecycle.ps1')

function Assert-True {
    param(
        [Parameter(Mandatory = $true)]
        [bool] $Condition,

        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Expected,

        [Parameter(Mandatory = $true)]
        [object] $Actual,

        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected'; found '$Actual'."
    }
}

$testParent = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path (Join-Path $repositoryRoot 'temp\temp-lifecycle-tests') `
    -AllowedTopLevelNames @('temp')
$testRoot = Join-Path $testParent ([Guid]::NewGuid().ToString('N'))
$fakeRepository = Join-Path $testRoot 'repository'
$junctionPath = $null

try {
    foreach ($whatIfArgument in @(
            '-Wi',
            '-Wi:$true',
            '-Wi:true',
            '-Wi:1',
            '-Wh',
            '-Wha',
            '-What',
            '-Whati',
            '-WhatIf',
            '-WhatIf:$true',
            '-WhatIf:true',
            '-WhatIf:1')) {
        Assert-True `
            -Condition (Test-ForwardedWhatIfRequest -Arguments @($whatIfArgument)) `
            -Message "WhatIf form '$whatIfArgument' was not recognized."
    }
    Assert-True `
        -Condition (-not (Test-ForwardedWhatIfRequest -Arguments @('-WarningAction', 'Stop'))) `
        -Message 'A non-WhatIf common parameter was mistaken for a preview.'
    foreach ($mutatingWhatIfArgument in @(
            '-Wi:$false',
            '-Wi:false',
            '-Wi:False',
            '-Wi:0',
            '-WhatIf:$false',
            '-WhatIf:false',
            '-WhatIf:False',
            '-WhatIf:0')) {
        Assert-True `
            -Condition (-not (Test-ForwardedWhatIfRequest -Arguments @($mutatingWhatIfArgument))) `
            -Message "Mutating WhatIf form '$mutatingWhatIfArgument' bypassed the workflow lease."
    }

    $previewRepository = Join-Path $testRoot 'preview-repository'
    $previewContext = Start-RepositoryTempWorkflowContext `
        -RepositoryRoot $previewRepository `
        -Preview
    try {
        Assert-Equal -Expected 'preview' -Actual ([string] $previewContext.mode) `
            -Message 'Preview context entered the wrong lifecycle mode.'
        Assert-True `
            -Condition (-not (Test-Path -LiteralPath $previewRepository)) `
            -Message 'Preview context created repository or lease content.'
    }
    finally {
        Stop-RepositoryTempWorkflowContext -Context $previewContext
    }

    $installRoot = Join-Path $fakeRepository 'temp\install'
    $exampleRoot = Join-Path $fakeRepository 'temp\e'
    $releaseScratchRoot = Join-Path $fakeRepository 'temp\release-candidate'
    $null = New-Item -ItemType Directory -Path $installRoot -Force
    $null = New-Item -ItemType Directory -Path $exampleRoot -Force
    $null = New-Item -ItemType Directory -Path $releaseScratchRoot -Force

    $installNames = @(
        'run-20260101-010101-001',
        'run-20260102-010101-001',
        'run-20260103-010101-001'
    )
    for ($index = 0; $index -lt $installNames.Count; $index += 1) {
        $path = Join-Path $installRoot $installNames[$index]
        $null = New-Item -ItemType Directory -Path $path
        [System.IO.File]::WriteAllText(
            (Join-Path $path 'diagnostic.txt'),
            $installNames[$index])
        (Get-Item -LiteralPath $path).LastWriteTimeUtc = [DateTime]::UtcNow.AddHours($index - 4)
    }
    $unrecognized = Join-Path $installRoot 'manual-diagnostics'
    $null = New-Item -ItemType Directory -Path $unrecognized

    foreach ($name in @('11111111', '22222222')) {
        $path = Join-Path $exampleRoot $name
        $null = New-Item -ItemType Directory -Path $path
        (Get-Item -LiteralPath $path).LastWriteTimeUtc = if ($name -eq '22222222') {
            [DateTime]::UtcNow
        }
        else {
            [DateTime]::UtcNow.AddHours(-1)
        }
    }

    foreach ($name in @(
            'previous-20260101-010101-001',
            'previous-20260102-010101-001')) {
        $path = Join-Path $releaseScratchRoot $name
        $null = New-Item -ItemType Directory -Path $path
        (Get-Item -LiteralPath $path).LastWriteTimeUtc = if ($name -like '*20260101*') {
            [DateTime]::UtcNow.AddHours(-2)
        }
        else {
            [DateTime]::UtcNow.AddHours(-1)
        }
    }

    $retention = Invoke-RepositoryTempRunRetention `
        -RepositoryRoot $fakeRepository `
        -KeepLatest 1
    Assert-Equal -Expected 5 -Actual $retention.removed.Count `
        -Message 'Retention removed an unexpected number of superseded runs.'
    Assert-Equal -Expected 2 -Actual $retention.retained.Count `
        -Message 'Before-run retention preserved an unexpected number of diagnostics.'
    Assert-True `
        -Condition (Test-Path -LiteralPath (Join-Path $installRoot $installNames[-1]) -PathType Container) `
        -Message 'The latest install diagnostic was removed.'
    Assert-True `
        -Condition (-not (Test-Path -LiteralPath (Join-Path $exampleRoot '22222222'))) `
        -Message 'Before-run retention kept a superseded heavy example run.'
    Assert-True `
        -Condition (Test-Path -LiteralPath $unrecognized -PathType Container) `
        -Message 'An unrecognized temp directory was removed.'
    Assert-True `
        -Condition (Test-Path -LiteralPath (
            Join-Path $releaseScratchRoot 'previous-20260102-010101-001') -PathType Container) `
        -Message 'The only release recovery copy was removed while no current release existed.'

    $null = New-Item -ItemType Directory `
        -Path (Join-Path $fakeRepository 'artifacts\release') `
        -Force
    $releaseRetention = Invoke-RepositoryTempRunRetention `
        -RepositoryRoot $fakeRepository `
        -KeepLatest 1
    Assert-Equal -Expected 1 -Actual $releaseRetention.removed.Count `
        -Message 'A superseded previous release copy remained beside a current release.'
    Assert-True `
        -Condition (-not (Test-Path -LiteralPath (
            Join-Path $releaseScratchRoot 'previous-20260102-010101-001'))) `
        -Message 'Current and previous release copies were retained together.'

    $whatIfOldRun = Join-Path $installRoot 'run-20251231-010101-001'
    $null = New-Item -ItemType Directory -Path $whatIfOldRun
    (Get-Item -LiteralPath $whatIfOldRun).LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(-10)
    $null = Invoke-RepositoryTempRunRetention `
        -RepositoryRoot $fakeRepository `
        -KeepLatest 1 `
        -WhatIf
    Assert-True -Condition (Test-Path -LiteralPath $whatIfOldRun -PathType Container) `
        -Message 'WhatIf retention changed the filesystem.'

    $outsideTarget = Join-Path $testRoot 'outside-target'
    $outsideSentinel = Join-Path $outsideTarget 'must-survive.txt'
    $null = New-Item -ItemType Directory -Path $outsideTarget -Force
    [System.IO.File]::WriteAllText($outsideSentinel, 'outside fake repository')
    $junctionPath = Join-Path $installRoot 'run-20240101-010101-001'
    $null = New-Item -ItemType Junction -Path $junctionPath -Target $outsideTarget
    $newestSafeRun = Join-Path $installRoot 'run-20270101-010101-001'
    $null = New-Item -ItemType Directory -Path $newestSafeRun
    $junctionRetention = Invoke-RepositoryTempRunRetention `
        -RepositoryRoot $fakeRepository `
        -KeepLatest 1 `
        -WarningAction SilentlyContinue
    Assert-Equal -Expected 1 -Actual $junctionRetention.warnings.Count `
        -Message 'An unsafe reparse-point candidate did not produce one retention warning.'
    Assert-True -Condition (Test-Path -LiteralPath $junctionPath -PathType Container) `
        -Message 'An unsafe reparse-point candidate was removed.'
    Assert-True -Condition (Test-Path -LiteralPath $outsideSentinel -PathType Leaf) `
        -Message 'Retention followed a junction and changed content outside the fake repository.'
    Assert-True -Condition (Test-Path -LiteralPath $newestSafeRun -PathType Container) `
        -Message 'The newest safe run was not retained beside an unsafe older candidate.'
    [System.IO.Directory]::Delete($junctionPath, $false)
    $junctionPath = $null

    # An undeletable run from before the workflow must not consume the one-run
    # failure quota and cause the current diagnostic to be discarded.
    $junctionPath = Join-Path $exampleRoot 'ffffffff'
    $null = New-Item -ItemType Junction -Path $junctionPath -Target $outsideTarget
    (Get-Item -LiteralPath $junctionPath -Force).LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(1)
    $beforeFailureRetention = Invoke-RepositoryTempRunRetention `
        -RepositoryRoot $fakeRepository `
        -KeepLatest 1 `
        -Phase Before `
        -WarningAction SilentlyContinue
    Assert-Equal -Expected 1 -Actual $beforeFailureRetention.unremoved.Count `
        -Message 'Before-run retention did not identify its undeletable candidate.'

    $currentFailure = Join-Path $exampleRoot '66666666'
    $null = New-Item -ItemType Directory -Path $currentFailure
    $protectedFailureRetention = Invoke-RepositoryTempRunRetention `
        -RepositoryRoot $fakeRepository `
        -KeepLatest 1 `
        -Phase Failure `
        -ProtectedPaths @($beforeFailureRetention.unremoved) `
        -WarningAction SilentlyContinue
    Assert-True -Condition (Test-Path -LiteralPath $junctionPath -PathType Container) `
        -Message 'Failure retention removed a protected pre-workflow candidate.'
    Assert-True -Condition (Test-Path -LiteralPath $currentFailure -PathType Container) `
        -Message 'An undeletable older candidate displaced the current failure diagnostic.'
    $protectedExampleRetained = @($protectedFailureRetention.retained | Where-Object {
        $_.StartsWith(
            $exampleRoot + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)
    })
    Assert-Equal -Expected 2 -Actual $protectedExampleRetained.Count `
        -Message 'Protected failure retention kept an unexpected candidate set.'
    [System.IO.Directory]::Delete($junctionPath, $false)
    $junctionPath = $null

    $failedExampleOld = Join-Path $exampleRoot '33333333'
    $failedExampleNew = Join-Path $exampleRoot '44444444'
    $null = New-Item -ItemType Directory -Path $failedExampleOld
    $null = New-Item -ItemType Directory -Path $failedExampleNew
    (Get-Item -LiteralPath $failedExampleOld).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(-1)
    (Get-Item -LiteralPath $failedExampleNew).LastWriteTimeUtc = [DateTime]::UtcNow
    $null = Invoke-RepositoryTempRunRetention `
        -RepositoryRoot $fakeRepository `
        -KeepLatest 1 `
        -Phase Failure
    Assert-True -Condition (-not (Test-Path -LiteralPath $failedExampleOld)) `
        -Message 'Failure retention kept more than one heavy-run diagnostic.'
    Assert-True -Condition (Test-Path -LiteralPath $failedExampleNew -PathType Container) `
        -Message 'Failure retention removed the newest heavy-run diagnostic.'

    $null = Invoke-RepositoryTempRunRetention `
        -RepositoryRoot $fakeRepository `
        -KeepLatest 1 `
        -Phase Success
    Assert-True -Condition (-not (Test-Path -LiteralPath $failedExampleNew)) `
        -Message 'Success retention kept a multi-gigabyte-capable heavy run.'
    Assert-True -Condition (Test-Path -LiteralPath $newestSafeRun -PathType Container) `
        -Message 'Success retention removed the newest small install receipt.'

    $previousWorkflowValue = [Environment]::GetEnvironmentVariable(
        $script:TempWorkflowEnvironmentVariable,
        [EnvironmentVariableTarget]::Process)
    $lease = Enter-RepositoryTempWorkflow -RepositoryRoot $fakeRepository
    try {
        [Environment]::SetEnvironmentVariable(
            $script:TempWorkflowEnvironmentVariable,
            'temp-lifecycle-test-owner',
            [EnvironmentVariableTarget]::Process)
        Assert-True `
            -Condition (Test-RepositoryTempWorkflowHeld -RepositoryRoot $fakeRepository) `
            -Message 'An acquired temp workflow lease was not observable.'
        Assert-True `
            -Condition (Test-NestedRepositoryTempWorkflow -RepositoryRoot $fakeRepository) `
            -Message 'A release-style inherited workflow was not recognized as nested.'

        $nestedOldStage = Join-Path $releaseScratchRoot 'staging-20260101-010101-001'
        $nestedCurrentStage = Join-Path $releaseScratchRoot 'staging-20270101-010101-001'
        $null = New-Item -ItemType Directory -Path $nestedOldStage
        $null = New-Item -ItemType Directory -Path $nestedCurrentStage
        $nestedContext = Start-RepositoryTempWorkflowContext `
            -RepositoryRoot $fakeRepository
        try {
            Assert-Equal -Expected 'nested' -Actual ([string] $nestedContext.mode) `
                -Message 'Inherited release stage acquired a second owner context.'
            # dev.ps1 performs retention only for owner contexts. Both paths
            # must therefore survive while the outer release is still active.
            Assert-True -Condition (Test-Path -LiteralPath $nestedOldStage -PathType Container) `
                -Message 'Nested workflow pruned an outer release staging directory.'
            Assert-True -Condition (Test-Path -LiteralPath $nestedCurrentStage -PathType Container) `
                -Message 'Nested workflow removed the current release staging directory.'
        }
        finally {
            Stop-RepositoryTempWorkflowContext -Context $nestedContext
        }

        $secondLeaseFailed = $false
        try {
            $unexpectedLease = Enter-RepositoryTempWorkflow -RepositoryRoot $fakeRepository
            $unexpectedLease.Dispose()
        }
        catch {
            $secondLeaseFailed = $true
        }
        Assert-True -Condition $secondLeaseFailed `
            -Message 'A second temp workflow lease was acquired concurrently.'
    }
    finally {
        [Environment]::SetEnvironmentVariable(
            $script:TempWorkflowEnvironmentVariable,
            $previousWorkflowValue,
            [EnvironmentVariableTarget]::Process)
        $lease.Dispose()
    }
    Assert-True `
        -Condition (-not (Test-RepositoryTempWorkflowHeld -RepositoryRoot $fakeRepository)) `
        -Message 'The temp workflow lease remained locked after disposal.'
}
finally {
    if ($null -ne $junctionPath -and (Test-Path -LiteralPath $junctionPath)) {
        $junction = Get-Item -LiteralPath $junctionPath -Force
        if (($junction.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0) {
            throw "Expected cleanup target is no longer a reparse point: '$junctionPath'."
        }
        # Delete only the junction entry. Never recurse through its target.
        [System.IO.Directory]::Delete($junctionPath, $false)
    }
    if (Test-Path -LiteralPath $testRoot) {
        $safeTestRoot = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path $testRoot `
            -AllowedTopLevelNames @('temp')
        Assert-NoReparsePoints -Path $safeTestRoot -AnchorPath $repositoryRoot
        Remove-Item -LiteralPath $safeTestRoot -Recurse -Force
    }
}

Write-Host 'Repository temp lifecycle tests passed.'

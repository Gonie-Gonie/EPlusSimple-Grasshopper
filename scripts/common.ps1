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

function Test-ExactByteArray {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]] $Left,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]] $Right
    )

    if ($Left.Length -ne $Right.Length) {
        return $false
    }
    for ($index = 0; $index -lt $Left.Length; $index += 1) {
        if ($Left[$index] -ne $Right[$index]) {
            return $false
        }
    }
    return $true
}

function Write-ExclusiveFlushedBytes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]] $Bytes
    )

    $stream = New-Object System.IO.FileStream(
        $Path,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    try {
        $stream.Write($Bytes, 0, $Bytes.Length)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }
}

function Invoke-PackageLockCommitReplace {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourcePath,

        [Parameter(Mandatory = $true)]
        [string] $DestinationPath,

        [Parameter(Mandatory = $true)]
        [string] $BackupPath
    )

    [System.IO.File]::Replace(
        $SourcePath,
        $DestinationPath,
        $BackupPath,
        $true)
}

function Get-PackageLockWorkflowPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    $root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    $lockDirectory = Assert-RepositoryChildPath `
        -RepositoryRoot $root `
        -Path (Join-Path $root '.tools\package-lock-workflow') `
        -AllowedTopLevelNames @('.tools')
    Assert-NoReparsePoints -Path $lockDirectory -AnchorPath $root
    Ensure-Directory -Path $lockDirectory
    Assert-NoReparsePoints -Path $lockDirectory -AnchorPath $root
    return Join-Path $lockDirectory 'workflow.lock'
}

function Enter-TrackedPackageLockWorkflow {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    $lockPath = Get-PackageLockWorkflowPath -RepositoryRoot $RepositoryRoot
    Assert-NoReparsePoints -Path $lockPath -AnchorPath $RepositoryRoot
    try {
        return New-Object System.IO.FileStream(
            $lockPath,
            [System.IO.FileMode]::OpenOrCreate,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
    }
    catch [System.IO.IOException] {
        throw "Another setup, build, or lock-file normalization workflow is already running for '$RepositoryRoot'."
    }
}

function Assert-TrackedPackageLockWorkflow {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [System.IO.FileStream] $WorkflowLock
    )

    $expected = [System.IO.Path]::GetFullPath(
        (Get-PackageLockWorkflowPath -RepositoryRoot $RepositoryRoot))
    $actual = [System.IO.Path]::GetFullPath($WorkflowLock.Name)
    if (-not $actual.Equals($expected, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $WorkflowLock.CanRead -or
        -not $WorkflowLock.CanWrite) {
        throw 'The supplied package-lock workflow handle is invalid or belongs to another repository.'
    }
    $probe = $null
    try {
        $probe = New-Object System.IO.FileStream(
            $expected,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite)
    }
    catch [System.IO.IOException] {
        return
    }
    finally {
        if ($null -ne $probe) {
            $probe.Dispose()
        }
    }
    throw 'The supplied package-lock workflow handle does not hold the exclusive repository lease.'
}

function Assert-NoIncompletePackageLockNormalizationTransaction {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    $root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    $transactionRoot = Assert-RepositoryChildPath `
        -RepositoryRoot $root `
        -Path (Join-Path $root '.tools\package-lock-normalization') `
        -AllowedTopLevelNames @('.tools')
    Assert-NoReparsePoints -Path $transactionRoot -AnchorPath $root
    if (Test-Path -LiteralPath $transactionRoot) {
        if (-not (Test-Path -LiteralPath $transactionRoot -PathType Container)) {
            throw "NuGet lock-file transaction root is not a directory: '$transactionRoot'."
        }
        $incompleteTransaction = Get-ChildItem `
            -LiteralPath $transactionRoot `
            -Force |
            Select-Object -First 1
        if ($null -ne $incompleteTransaction) {
            throw (
                'An incomplete NuGet lock-file normalization transaction was found. ' +
                "Inspect and recover it before continuing: '$($incompleteTransaction.FullName)'.")
        }
    }
    return $transactionRoot
}

function Remove-PackageLockTransactionDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string] $TransactionPath
    )

    $root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    $safeTransaction = Assert-RepositoryChildPath `
        -RepositoryRoot $root `
        -Path $TransactionPath `
        -AllowedTopLevelNames @('.tools')
    Assert-NoReparsePoints -Path $safeTransaction -AnchorPath $root
    $items = @(Get-ChildItem -LiteralPath $safeTransaction -Force)
    foreach ($item in $items) {
        if ($item.PSIsContainer -or
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to clean an unexpected transaction entry: '$($item.FullName)'."
        }
    }
    foreach ($item in $items) {
        [System.IO.File]::Delete($item.FullName)
    }
    [System.IO.Directory]::Delete($safeTransaction, $false)
}

function Normalize-TrackedPackageLockLineEndings {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [AllowNull()]
        [System.IO.FileStream] $WorkflowLock
    )

    $ErrorActionPreference = 'Stop'

    $root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        throw "Repository root does not exist: '$root'."
    }

    $ownedWorkflowLock = $null
    if ($null -eq $WorkflowLock) {
        if (-not $WhatIfPreference) {
            $ownedWorkflowLock = Enter-TrackedPackageLockWorkflow -RepositoryRoot $root
            $WorkflowLock = $ownedWorkflowLock
        }
    }
    else {
        Assert-TrackedPackageLockWorkflow `
            -RepositoryRoot $root `
            -WorkflowLock $WorkflowLock
    }

    $transactionPath = $null
    $entries = New-Object System.Collections.ArrayList
    $failure = $null
    $rollbackFailures = New-Object System.Collections.ArrayList
    $recoveryRequired = $false
    $completed = $false
    try {
        $git = Get-Command git.exe -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -eq $git) {
            throw 'Git is required to enumerate tracked NuGet lock files.'
        }

        $tracked = @(& $git.Source -C $root ls-files -- '*packages.lock.json')
        if ($LASTEXITCODE -ne 0) {
            throw "Git could not enumerate tracked NuGet lock files under '$root'."
        }
        $tracked = @($tracked |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique)
        if ($tracked.Count -eq 0) {
            throw "No tracked packages.lock.json files were found under '$root'."
        }

        $rootPrefix = $root + [System.IO.Path]::DirectorySeparatorChar
        foreach ($relativePath in $tracked) {
            if ([System.IO.Path]::IsPathRooted($relativePath) -or
                $relativePath.IndexOf([char]0) -ge 0) {
                throw "Git returned an unsafe tracked lock-file path: '$relativePath'."
            }
            $segments = @($relativePath.Split(
                @('/', '\'),
                [System.StringSplitOptions]::RemoveEmptyEntries))
            if ($segments.Count -eq 0 -or
                $segments -contains '..' -or
                $segments -contains '.') {
                throw "Git returned an unsafe tracked lock-file path: '$relativePath'."
            }

            $path = [System.IO.Path]::GetFullPath((Join-Path $root $relativePath))
            if (-not $path.StartsWith(
                $rootPrefix,
                [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Tracked lock file escaped the repository: '$relativePath'."
            }
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "Tracked lock file is missing or not a regular file: '$relativePath'."
            }
            Assert-NoReparsePoints -Path $path -AnchorPath $root

            $bytes = [System.IO.File]::ReadAllBytes($path)
            if ($bytes.Length -ge 3 -and
                $bytes[0] -eq 0xEF -and
                $bytes[1] -eq 0xBB -and
                $bytes[2] -eq 0xBF) {
                throw "Tracked lock file contains a forbidden UTF-8 BOM: '$relativePath'."
            }

            $normalized = New-Object 'System.Collections.Generic.List[byte]'
            $sawCrLf = $false
            $sawLf = $false
            for ($index = 0; $index -lt $bytes.Length; $index += 1) {
                $value = $bytes[$index]
                if ($value -eq 13) {
                    if ($index + 1 -ge $bytes.Length -or $bytes[$index + 1] -ne 10) {
                        throw "Tracked lock file contains a lone CR byte: '$relativePath'."
                    }
                    $normalized.Add([byte] 10)
                    $sawCrLf = $true
                    $index += 1
                }
                elseif ($value -eq 10) {
                    $normalized.Add([byte] 10)
                    $sawLf = $true
                }
                else {
                    $normalized.Add($value)
                }
            }
            if ($sawCrLf -and $sawLf) {
                throw "Tracked lock file contains mixed LF and CRLF endings: '$relativePath'."
            }
            $null = $entries.Add([pscustomobject] [ordered] @{
                RelativePath = [string] $relativePath
                Path = $path
                OriginalBytes = [byte[]] $bytes
                NormalizedBytes = [byte[]] $normalized.ToArray()
                RollbackBytes = [byte[]] $bytes
                NeedsChange = [bool] $sawCrLf
                Attempted = $false
                ConcurrentChangeDetected = $false
                TransactionIndex = -1
                StagePath = $null
                OriginalPath = $null
                ReplaceBackupPath = $null
                RollbackDiscardPath = $null
            })
        }

        $transactionRoot = Assert-NoIncompletePackageLockNormalizationTransaction `
            -RepositoryRoot $root

        $changedEntries = @($entries | Where-Object { $_.NeedsChange })
        if ($WhatIfPreference) {
            foreach ($entry in $changedEntries) {
                Write-Host "What if: normalize tracked NuGet lock file '$($entry.RelativePath)' to LF."
            }
            Write-Host "NuGet lock-file LF normalization: 0 changed, $($tracked.Count) checked."
            $completed = $true
        }
        elseif ($changedEntries.Count -eq 0) {
            Write-Host "NuGet lock-file LF normalization: 0 changed, $($tracked.Count) checked."
            $completed = $true
        }
        else {
            Ensure-Directory -Path $transactionRoot
            Assert-NoReparsePoints -Path $transactionRoot -AnchorPath $root
            $transactionPath = Join-Path $transactionRoot ([Guid]::NewGuid().ToString('N'))
            $null = New-Item -ItemType Directory -Path $transactionPath
            Assert-NoReparsePoints -Path $transactionPath -AnchorPath $root

            $manifestEntries = New-Object System.Collections.ArrayList
            for ($entryIndex = 0; $entryIndex -lt $changedEntries.Count; $entryIndex += 1) {
                $entry = $changedEntries[$entryIndex]
                $prefix = $entryIndex.ToString('D4')
                $entry.TransactionIndex = $entryIndex
                $entry.StagePath = Join-Path $transactionPath ($prefix + '.normalized.tmp')
                $entry.OriginalPath = Join-Path $transactionPath ($prefix + '.original.bin')
                $entry.ReplaceBackupPath = Join-Path $transactionPath ($prefix + '.replace.bak')
                $entry.RollbackDiscardPath = Join-Path $transactionPath (
                    $prefix + '.rollback-discard.bin')
                Write-ExclusiveFlushedBytes `
                    -Path $entry.StagePath `
                    -Bytes $entry.NormalizedBytes
                Write-ExclusiveFlushedBytes `
                    -Path $entry.OriginalPath `
                    -Bytes $entry.OriginalBytes
                $stagedBytes = [System.IO.File]::ReadAllBytes($entry.StagePath)
                $snapshotBytes = [System.IO.File]::ReadAllBytes($entry.OriginalPath)
                if (-not (Test-ExactByteArray `
                    -Left $stagedBytes `
                    -Right $entry.NormalizedBytes) -or
                    -not (Test-ExactByteArray `
                    -Left $snapshotBytes `
                    -Right $entry.OriginalBytes)) {
                    throw "Transaction staging verification failed: '$($entry.RelativePath)'."
                }
                $null = $manifestEntries.Add([pscustomobject] [ordered] @{
                    index = $entryIndex
                    path = $entry.RelativePath
                    original = [System.IO.Path]::GetFileName($entry.OriginalPath)
                    originalSha256 = Get-Sha256 -Path $entry.OriginalPath
                    replacement = [System.IO.Path]::GetFileName($entry.StagePath)
                    replacementSha256 = Get-Sha256 -Path $entry.StagePath
                    replaceBackup = [System.IO.Path]::GetFileName(
                        $entry.ReplaceBackupPath)
                    rollbackDiscard = [System.IO.Path]::GetFileName(
                        $entry.RollbackDiscardPath)
                })
            }
            $manifest = [pscustomobject] [ordered] @{
                schema = 'goniegonie.package-lock-normalization-transaction.v1'
                repository = $root
                files = @($manifestEntries)
            }
            $manifestBytes = [System.Text.Encoding]::UTF8.GetBytes(
                (($manifest | ConvertTo-Json -Depth 5) + "`n"))
            Write-ExclusiveFlushedBytes `
                -Path (Join-Path $transactionPath 'transaction.json') `
                -Bytes $manifestBytes

            foreach ($entry in $entries) {
                Assert-NoReparsePoints -Path $entry.Path -AnchorPath $root
                $current = [System.IO.File]::ReadAllBytes($entry.Path)
                if (-not (Test-ExactByteArray -Left $current -Right $entry.OriginalBytes)) {
                    throw "Tracked lock file changed during normalization preflight: '$($entry.RelativePath)'."
                }
            }

            foreach ($entry in $changedEntries) {
                Assert-NoReparsePoints -Path $entry.Path -AnchorPath $root
                $current = [System.IO.File]::ReadAllBytes($entry.Path)
                if (-not (Test-ExactByteArray -Left $current -Right $entry.OriginalBytes)) {
                    throw "Tracked lock file changed before atomic replacement: '$($entry.RelativePath)'."
                }
                Assert-NoReparsePoints -Path $entry.StagePath -AnchorPath $root
                Assert-NoReparsePoints -Path $entry.OriginalPath -AnchorPath $root
                $stagedBytes = [System.IO.File]::ReadAllBytes($entry.StagePath)
                $snapshotBytes = [System.IO.File]::ReadAllBytes($entry.OriginalPath)
                if (-not (Test-ExactByteArray `
                    -Left $stagedBytes `
                    -Right $entry.NormalizedBytes) -or
                    -not (Test-ExactByteArray `
                    -Left $snapshotBytes `
                    -Right $entry.OriginalBytes)) {
                    throw "Transaction files changed before atomic replacement: '$($entry.RelativePath)'."
                }
                $entry.Attempted = $true
                Invoke-PackageLockCommitReplace `
                    -SourcePath $entry.StagePath `
                    -DestinationPath $entry.Path `
                    -BackupPath $entry.ReplaceBackupPath
                if (-not (Test-Path -LiteralPath $entry.ReplaceBackupPath -PathType Leaf)) {
                    throw "Atomic replacement did not preserve its backup: '$($entry.RelativePath)'."
                }
                $replaceBackup = [System.IO.File]::ReadAllBytes(
                    $entry.ReplaceBackupPath)
                $entry.RollbackBytes = [byte[]] $replaceBackup
                if (-not (Test-ExactByteArray `
                    -Left $replaceBackup `
                    -Right $entry.OriginalBytes)) {
                    $entry.ConcurrentChangeDetected = $true
                    $recoveryRequired = $true
                    throw (
                        'A concurrent change was captured by the atomic replacement backup: ' +
                        "'$($entry.RelativePath)'.")
                }
                Assert-NoReparsePoints -Path $entry.Path -AnchorPath $root
                $actual = [System.IO.File]::ReadAllBytes($entry.Path)
                if (-not (Test-ExactByteArray -Left $actual -Right $entry.NormalizedBytes)) {
                    throw "Atomic lock-file normalization verification failed: '$($entry.RelativePath)'."
                }
            }

            foreach ($entry in $entries) {
                Assert-NoReparsePoints -Path $entry.Path -AnchorPath $root
                $expected = if ($entry.NeedsChange) {
                    $entry.NormalizedBytes
                }
                else {
                    $entry.OriginalBytes
                }
                $actual = [System.IO.File]::ReadAllBytes($entry.Path)
                if (-not (Test-ExactByteArray -Left $actual -Right $expected)) {
                    throw "Batch lock-file normalization verification failed: '$($entry.RelativePath)'."
                }
            }
            Write-Host "NuGet lock-file LF normalization: $($changedEntries.Count) changed, $($tracked.Count) checked."
            $completed = $true
        }
    }
    catch {
        $failure = $_
        $attempted = @($entries | Where-Object { $_.Attempted })
        for ($rollbackIndex = $attempted.Count - 1; $rollbackIndex -ge 0; $rollbackIndex -= 1) {
            $entry = $attempted[$rollbackIndex]
            try {
                if (Test-Path -LiteralPath $entry.ReplaceBackupPath -PathType Leaf) {
                    Assert-NoReparsePoints `
                        -Path $entry.ReplaceBackupPath `
                        -AnchorPath $root
                    $entry.RollbackBytes = [byte[]] [System.IO.File]::ReadAllBytes(
                        $entry.ReplaceBackupPath)
                    if (-not (Test-ExactByteArray `
                        -Left $entry.RollbackBytes `
                        -Right $entry.OriginalBytes)) {
                        $entry.ConcurrentChangeDetected = $true
                        $recoveryRequired = $true
                    }
                }
                Assert-NoReparsePoints -Path $entry.Path -AnchorPath $root
                $current = [System.IO.File]::ReadAllBytes($entry.Path)
                if (-not (Test-ExactByteArray `
                    -Left $current `
                    -Right $entry.RollbackBytes)) {
                    if (-not (Test-ExactByteArray `
                        -Left $current `
                        -Right $entry.NormalizedBytes)) {
                        throw (
                            'Refusing to overwrite an ambiguously changed lock file during rollback: ' +
                            "'$($entry.RelativePath)'.")
                    }
                    $rollbackStage = Join-Path $transactionPath (
                        ([Guid]::NewGuid().ToString('N')) + '.rollback.tmp')
                    Write-ExclusiveFlushedBytes `
                        -Path $rollbackStage `
                        -Bytes $entry.RollbackBytes
                    $discard = $entry.RollbackDiscardPath
                    [System.IO.File]::Replace($rollbackStage, $entry.Path, $discard, $true)
                    $discardBytes = [System.IO.File]::ReadAllBytes($discard)
                    if (-not (Test-ExactByteArray `
                        -Left $discardBytes `
                        -Right $current)) {
                        $entry.ConcurrentChangeDetected = $true
                        $recoveryRequired = $true
                    }
                }
                Assert-NoReparsePoints -Path $entry.Path -AnchorPath $root
                $restored = [System.IO.File]::ReadAllBytes($entry.Path)
                if (-not (Test-ExactByteArray `
                    -Left $restored `
                    -Right $entry.RollbackBytes)) {
                    throw "Rollback verification failed for '$($entry.RelativePath)'."
                }
            }
            catch {
                $null = $rollbackFailures.Add($_)
            }
        }
    }
    finally {
        $keepRecovery = $rollbackFailures.Count -gt 0 -or $recoveryRequired
        if (-not [string]::IsNullOrWhiteSpace($transactionPath) -and
            (Test-Path -LiteralPath $transactionPath) -and
            -not $keepRecovery) {
            try {
                Remove-PackageLockTransactionDirectory `
                    -RepositoryRoot $root `
                    -TransactionPath $transactionPath
            }
            catch {
                $null = $rollbackFailures.Add($_)
            }
        }
        if ($null -ne $ownedWorkflowLock) {
            $ownedWorkflowLock.Dispose()
        }
    }

    if ($rollbackFailures.Count -gt 0) {
        $reason = if ($null -eq $failure) {
            'transaction cleanup failed'
        }
        else {
            $failure.Exception.Message
        }
        throw "NuGet lock-file normalization could not recover completely: $reason. Recovery files were retained at '$transactionPath'."
    }
    if ($recoveryRequired) {
        $reason = if ($null -eq $failure) {
            'a concurrent change was detected'
        }
        else {
            $failure.Exception.Message
        }
        throw "NuGet lock-file normalization stopped after preserving a concurrent update: $reason. Recovery files were retained at '$transactionPath'."
    }
    if ($null -ne $failure) {
        throw $failure
    }
    if (-not $completed) {
        throw 'NuGet lock-file normalization ended without a verified result.'
    }
}

function Invoke-WithTrackedPackageLockNormalization {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [scriptblock] $Action
    )

    $workflowLock = $null
    if (-not $WhatIfPreference) {
        $workflowLock = Enter-TrackedPackageLockWorkflow -RepositoryRoot $RepositoryRoot
    }
    $actionFailure = $null
    $normalizationFailure = $null
    try {
        $null = Assert-NoIncompletePackageLockNormalizationTransaction `
            -RepositoryRoot $RepositoryRoot
        try {
            & $Action
        }
        catch {
            $actionFailure = $_
        }
        try {
            Normalize-TrackedPackageLockLineEndings `
                -RepositoryRoot $RepositoryRoot `
                -WorkflowLock $workflowLock
        }
        catch {
            $normalizationFailure = $_
        }
    }
    finally {
        if ($null -ne $workflowLock) {
            $workflowLock.Dispose()
        }
    }

    if ($null -ne $actionFailure -and $null -ne $normalizationFailure) {
        throw (
            $actionFailure.Exception.Message +
            ' Lock-file normalization also failed: ' +
            $normalizationFailure.Exception.Message)
    }
    if ($null -ne $actionFailure) {
        throw $actionFailure
    }
    if ($null -ne $normalizationFailure) {
        throw $normalizationFailure
    }
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

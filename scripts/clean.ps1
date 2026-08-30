#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [switch] $TempOnly,
    [switch] $ArtifactsOnly,
    [switch] $CachesOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

$exclusiveOptions = @(@($TempOnly, $ArtifactsOnly, $CachesOnly) | Where-Object { $_ }).Count
if ($exclusiveOptions -gt 1) {
    throw '-TempOnly, -ArtifactsOnly, and -CachesOnly cannot be used together.'
}

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
$cleanTemp = -not ($ArtifactsOnly -or $CachesOnly)
$cleanArtifacts = -not ($TempOnly -or $CachesOnly)
$cleanCaches = -not ($TempOnly -or $ArtifactsOnly)

if ($cleanTemp) {
    $tempPath = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path (Join-Path $repositoryRoot 'temp') `
        -AllowedTopLevelNames @('temp')

    if (Test-Path -LiteralPath $tempPath) {
        if ($PSCmdlet.ShouldProcess($tempPath, 'Remove disposable temp tree')) {
            $workflowLock = Enter-TrackedPackageLockWorkflow `
                -RepositoryRoot $repositoryRoot
            try {
                Assert-NoReparsePoints -Path $tempPath -AnchorPath $repositoryRoot
                Remove-Item -LiteralPath $tempPath -Recurse -Force
                Write-Host "Removed disposable tree: $tempPath"
            }
            finally {
                $workflowLock.Dispose()
            }
        }
    }
    else {
        Write-Host "Already clean: $tempPath"
    }
}

if ($cleanArtifacts) {
    $artifactsPath = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path (Join-Path $repositoryRoot 'artifacts') `
        -AllowedTopLevelNames @('artifacts')

    if (Test-Path -LiteralPath $artifactsPath -PathType Container) {
        Assert-NoReparsePoints -Path $artifactsPath -AnchorPath $repositoryRoot
        $generatedItems = @(Get-ChildItem -LiteralPath $artifactsPath -Force |
            Where-Object { -not $_.Name.Equals('README.md', [System.StringComparison]::OrdinalIgnoreCase) })

        foreach ($item in $generatedItems) {
            $safeItem = Assert-RepositoryChildPath `
                -RepositoryRoot $repositoryRoot `
                -Path $item.FullName `
                -AllowedTopLevelNames @('artifacts')
            if ($PSCmdlet.ShouldProcess($safeItem, 'Remove generated artifact')) {
                Remove-Item -LiteralPath $safeItem -Recurse -Force
                Write-Host "Removed generated artifact: $safeItem"
            }
        }

        if ($generatedItems.Count -eq 0) {
            Write-Host "Already clean: $artifactsPath (README.md preserved)"
        }
    }
    else {
        Write-Host "Already clean: $artifactsPath"
    }
}

if ($cleanCaches) {
    $cacheNames = @('__pycache__', '.pytest_cache', 'TestResults', 'bin', 'obj')
    $cacheRoots = @('scripts', 'src', 'tests', 'tools')
    $gitExecutable = (Get-Command git -CommandType Application -ErrorAction Stop).Source
    $cacheDirectories = @()
    foreach ($relativeRoot in $cacheRoots) {
        $cacheRoot = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path (Join-Path $repositoryRoot $relativeRoot) `
            -AllowedTopLevelNames $cacheRoots
        if (Test-Path -LiteralPath $cacheRoot -PathType Container) {
            $cacheDirectories += @(Get-ChildItem -LiteralPath $cacheRoot -Directory -Force -Recurse |
                Where-Object { $_.Name -in $cacheNames })
        }
    }

    $cacheDirectories = @($cacheDirectories |
        Sort-Object @{ Expression = { $_.FullName.Length }; Descending = $true }, FullName -Unique)
    foreach ($cacheDirectory in $cacheDirectories) {
        if (-not (Test-Path -LiteralPath $cacheDirectory.FullName -PathType Container)) {
            continue
        }
        $safeCache = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path $cacheDirectory.FullName `
            -AllowedTopLevelNames $cacheRoots
        $relativeCache = $safeCache.Substring($repositoryRoot.Length + 1) -replace '\\', '/'
        $trackedEntries = @(& $gitExecutable `
                --literal-pathspecs `
                -C $repositoryRoot `
                ls-files `
                -- `
                $relativeCache)
        if ($LASTEXITCODE -ne 0) {
            throw "Could not verify tracked content below cache candidate '$relativeCache'."
        }
        if ($trackedEntries.Count -ne 0) {
            throw "Refusing to remove cache candidate with tracked content: '$relativeCache'."
        }
        & $gitExecutable -C $repositoryRoot check-ignore --quiet --no-index -- ($relativeCache + '/')
        if ($LASTEXITCODE -ne 0) {
            throw "Refusing to remove cache candidate not covered by .gitignore: '$relativeCache'."
        }
        if ($PSCmdlet.ShouldProcess($safeCache, 'Remove generated source-tree cache')) {
            Assert-NoReparsePoints -Path $safeCache -AnchorPath $repositoryRoot
            Remove-Item -LiteralPath $safeCache -Recurse -Force
            Write-Host "Removed generated cache: $safeCache"
        }
    }

    if ($cacheDirectories.Count -eq 0) {
        Write-Host 'Source-tree caches are already clean.'
    }
}

Write-Host 'Clean complete. Repository-local toolchains under .tools were retained.'

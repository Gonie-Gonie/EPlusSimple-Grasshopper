#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [switch] $TempOnly,
    [switch] $ArtifactsOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

if ($TempOnly -and $ArtifactsOnly) {
    throw '-TempOnly and -ArtifactsOnly cannot be used together.'
}

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
$cleanTemp = -not $ArtifactsOnly
$cleanArtifacts = -not $TempOnly

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

Write-Host 'Clean complete. Repository-local toolchains under .tools were retained.'

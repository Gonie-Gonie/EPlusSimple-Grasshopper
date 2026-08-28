#requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackagesRoot,

    [Parameter(Mandatory = $true)]
    [string] $SpecPath,

    [Parameter(Mandatory = $true)]
    [string] $DotNetExecutable,

    [string] $ReportPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $repositoryRoot 'scripts\common.ps1')
$distributionManifestPath = Join-Path $repositoryRoot 'runtime\distributions.json'

$spec = Get-Content -LiteralPath $SpecPath -Raw | ConvertFrom-Json
$version = [string] $spec.version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Package specification version is empty.'
}

$propsText = [System.IO.File]::ReadAllText((Join-Path $repositoryRoot 'Directory.Build.props'))
foreach ($expected in @(
    "<VersionPrefix>$version</VersionPrefix>",
    "<AssemblyVersion>$version.0</AssemblyVersion>",
    "<FileVersion>$version.0</FileVersion>")) {
    if (-not $propsText.Contains($expected)) {
        throw "Central build metadata is missing '$expected'."
    }
}

foreach ($packageInfo in @(
    'src\InvisibleDragon\GonieGonie.InvisibleDragon.Core\PackageInfo.cs',
    'src\SimpleDragon\GonieGonie.SimpleDragon.Core\PackageInfo.cs')) {
    $text = [System.IO.File]::ReadAllText((Join-Path $repositoryRoot $packageInfo))
    if ($text -notmatch ('public const string Version\s*=\s*"' + [regex]::Escape($version) + '";')) {
        throw "PackageInfo version does not match $version in '$packageInfo'."
    }
}

foreach ($product in @($spec.products)) {
    $manifestPath = Join-Path $repositoryRoot (Join-Path 'packaging' (Join-Path ([string] $product.id) 'manifest.yml'))
    $manifest = [System.IO.File]::ReadAllText($manifestPath)
    if ($manifest -notmatch ('(?m)^name:\s*' + [regex]::Escape([string] $product.id) + '\s*$') -or
        $manifest -notmatch ('(?m)^version:\s*' + [regex]::Escape($version) + '\s*$') -or
        $manifest -notmatch '(?m)^icon:\s*icon\.png\s*$') {
        throw "Source Yak manifest identity/version/icon mismatch: '$manifestPath'."
    }
}

foreach ($path in @(Get-ChildItem -LiteralPath $PackagesRoot -Recurse -Force)) {
    $relative = $path.FullName.Substring([System.IO.Path]::GetFullPath($PackagesRoot).TrimEnd('\', '/').Length)
    if ($relative -match '(?i)(?:^|[\\/])(idragon|epsimple|snu-bslab)(?:[\\/]|$)') {
        throw "Generated package path uses a non-product ownership/name segment: '$relative'."
    }
}

Set-RepositoryBuildEnvironment -RepositoryRoot $repositoryRoot -DotNetExecutable $DotNetExecutable
$project = Join-Path $repositoryRoot 'tools\package-verify\GonieGonie.PackageVerifier.csproj'
$buildLog = Join-Path $repositoryRoot 'temp\packaging\logs\package-verifier-build.log'
Invoke-LoggedNativeCommand `
    -FilePath $DotNetExecutable `
    -ArgumentList @('build', $project, '--configuration', 'Release', '--nologo') `
    -LogPath $buildLog `
    -FailureMessage 'Package verifier build failed'

$arguments = @(
    'run',
    '--project', $project,
    '--configuration', 'Release',
    '--no-build',
    '--',
    '--packages-root', [System.IO.Path]::GetFullPath($PackagesRoot),
    '--spec', [System.IO.Path]::GetFullPath($SpecPath),
    '--distributions', [System.IO.Path]::GetFullPath($distributionManifestPath))
if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    $arguments += @('--report', [System.IO.Path]::GetFullPath($ReportPath))
}

$verifyLog = Join-Path $repositoryRoot 'temp\packaging\logs\package-verifier-run.log'
Invoke-LoggedNativeCommand `
    -FilePath $DotNetExecutable `
    -ArgumentList $arguments `
    -LogPath $verifyLog `
    -FailureMessage 'Package layout/shared-assembly compatibility tests failed'

Write-Host 'Package tests passed: layout, product-exclusive embedded archives, exact SHA/size pins, ZIP safety, KoreanTMY 80/78 coverage, EnergyPlus license identity, no expanded EP/EPW or Python, shared identity, and versions.'

#requires -Version 5.1

[CmdletBinding()]
param(
    [ValidateSet('Release')]
    [string] $Configuration = 'Release',

    [switch] $SkipBuild,
    [switch] $NoRestore,
    [switch] $RunPortableHostGate
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$packagesRoot = Join-Path $artifactsRoot 'packages'
$workingRoot = Join-Path $repositoryRoot 'temp\packaging'
$specPath = Join-Path $repositoryRoot 'packaging\package-spec.json'
$settingsPath = Join-Path $repositoryRoot '.config\local.settings.json'
$licensePath = Join-Path $repositoryRoot 'LICENSE'
$noticePath = Join-Path $repositoryRoot 'NOTICE.md'
$distributionManifestPath = Join-Path $repositoryRoot 'runtime\distributions.json'
$distributionRoot = Join-Path $repositoryRoot '.tools\distributions'

function Reset-GeneratedDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $AllowedTopLevelName
    )

    $safePath = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $Path `
        -AllowedTopLevelNames @($AllowedTopLevelName)
    if (Test-Path -LiteralPath $safePath) {
        Assert-NoReparsePoints -Path $safePath -AnchorPath $repositoryRoot
        Remove-Item -LiteralPath $safePath -Recurse -Force
    }
    Ensure-Directory -Path $safePath
}

function Write-Utf8Text {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Content
    )

    Ensure-Directory -Path (Split-Path -Parent $Path)
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8WithoutBom)
}

function Get-RelativeUnixPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $normalizedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $normalizedPath = [System.IO.Path]::GetFullPath($Path)
    $prefix = $normalizedRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $normalizedPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "'$normalizedPath' is not below '$normalizedRoot'."
    }

    return $normalizedPath.Substring($prefix.Length) -replace '\\', '/'
}

function Write-Checksums {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root
    )

    $checksumPath = Join-Path $Root 'checksums.sha256'
    $lines = foreach ($file in @(Get-ChildItem -LiteralPath $Root -File -Recurse |
        Where-Object { $_.FullName -ne $checksumPath } |
        Sort-Object FullName)) {
        '{0}  {1}' -f (Get-Sha256 -Path $file.FullName), (Get-RelativeUnixPath -Root $Root -Path $file.FullName)
    }
    Write-Utf8Text -Path $checksumPath -Content (($lines -join [Environment]::NewLine) + [Environment]::NewLine)
}

function Copy-PackageRootFiles {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Product,

        [Parameter(Mandatory = $true)]
        [string] $Destination,

        [Parameter(Mandatory = $true)]
        [string] $PayloadDescription
    )

    Ensure-Directory -Path $Destination
    $sourceRoot = Join-Path $repositoryRoot (Join-Path 'packaging' ([string] $Product.id))
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'manifest.yml') -Destination (Join-Path $Destination 'manifest.yml') -Force
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'icon.png') -Destination (Join-Path $Destination 'icon.png') -Force
    Copy-Item -LiteralPath $licensePath -Destination (Join-Path $Destination 'LICENSE.txt') -Force
    Copy-Item -LiteralPath $noticePath -Destination (Join-Path $Destination 'NOTICE.md') -Force

    $embeddedDescription = if ([string] $Product.id -eq 'invisible-dragon') {
        'It includes the exact, hash-verified official EnergyPlus 24.2.0 Windows archive under `runtime/energyplus/`, plus `runtime/energyplus/LICENSE.txt` copied byte-for-byte from that archive. The runtime bootstrap extracts and validates the archive when needed.'
    }
    else {
        'It includes the exact, hash-verified KoreanTMY v1 weather archive under `runtime/weather/`. SimpleDragon resolves the tracked address metadata against that embedded archive.'
    }
    $readme = @"
# $($Product.display_name) $($spec.version)

This is a Gonie-Gonie $PayloadDescription for Grasshopper on Windows.

It contains managed plugin/runtime-bootstrap assemblies and one product-specific
embedded distribution archive. $embeddedDescription RhinoCommon, Grasshopper,
debug symbols, XML documentation, Python, directly expanded EnergyPlus files,
and directly expanded EPW files are excluded.

See `package-manifest.json`, `checksums.sha256`, `LICENSE.txt`, and `NOTICE.md`
in this directory for identity, integrity, licensing, and provenance details.
"@
    Write-Utf8Text -Path (Join-Path $Destination 'README.md') -Content ($readme.Trim() + [Environment]::NewLine)
}

function Get-ProductDistribution {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProductId
    )

    $matches = @($distributionManifest.payloads | Where-Object { [string] $_.product -eq $ProductId })
    if ($matches.Count -ne 1) {
        throw "Expected exactly one embedded distribution for '$ProductId'; found $($matches.Count)."
    }
    return $matches[0]
}

function Copy-EmbeddedDistribution {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Product,

        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    $distribution = Get-ProductDistribution -ProductId ([string] $Product.id)
    $source = Join-Path $distributionRoot (([string] $distribution.developmentPath).Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Verified embedded payload is missing: '$source'. Run 'dev.cmd setup' without -SkipEmbeddedPayloads."
    }
    $sourceItem = Get-Item -LiteralPath $source
    if ([int64] $sourceItem.Length -ne [int64] $distribution.size -or
        (Get-Sha256 -Path $source).ToLowerInvariant() -ne ([string] $distribution.sha256).ToLowerInvariant()) {
        throw "Embedded payload identity mismatch: '$source'. Rerun 'dev.cmd setup'."
    }

    $relativePath = [string] $distribution.packagePath
    if ($relativePath -ne ('runtime/' + $(if ([string] $Product.id -eq 'invisible-dragon') { 'energyplus/' } else { 'weather/' }) + [string] $distribution.fileName)) {
        throw "Embedded payload path/product contract mismatch for '$($Product.id)': '$relativePath'."
    }
    $target = Join-Path $Destination ($relativePath.Replace('/', '\'))
    if (Test-Path -LiteralPath $target) {
        throw "Embedded payload would be copied more than once at '$target'."
    }
    Ensure-Directory -Path (Split-Path -Parent $target)
    Copy-Item -LiteralPath $source -Destination $target
    if ([int64] (Get-Item -LiteralPath $target).Length -ne [int64] $distribution.size -or
        (Get-Sha256 -Path $target).ToLowerInvariant() -ne ([string] $distribution.sha256).ToLowerInvariant()) {
        throw "Copied embedded payload failed identity verification: '$target'."
    }

    $result = [ordered] @{
        id = [string] $distribution.id
        kind = [string] $distribution.kind
        path = $relativePath
        fileName = [string] $distribution.fileName
        size = [int64] $distribution.size
        sha256 = ([string] $distribution.sha256).ToLowerInvariant()
    }
    if ([string] $Product.id -eq 'invisible-dragon') {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::OpenRead($source)
        try {
            $entry = $archive.GetEntry([string] $distribution.licenseEntry)
            if ($null -eq $entry -or [int64] $entry.Length -ne [int64] $distribution.licenseSize) {
                throw 'Pinned EnergyPlus archive license entry is missing or has the wrong size.'
            }
            $licenseTarget = Join-Path $Destination (([string] $distribution.packageLicensePath).Replace('/', '\'))
            Ensure-Directory -Path (Split-Path -Parent $licenseTarget)
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $licenseTarget, $false)
        }
        finally {
            $archive.Dispose()
        }
        if ((Get-Sha256 -Path $licenseTarget).ToLowerInvariant() -ne ([string] $distribution.licenseSha256).ToLowerInvariant()) {
            throw 'Extracted EnergyPlus LICENSE.txt differs from the exact archive entry.'
        }
        $result.license = [pscustomobject] [ordered] @{
            archiveEntry = [string] $distribution.licenseEntry
            path = [string] $distribution.packageLicensePath
            size = [int64] $distribution.licenseSize
            sha256 = ([string] $distribution.licenseSha256).ToLowerInvariant()
        }
    }

    return [pscustomobject] $result
}

function Copy-FrameworkPayload {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Product,

        [Parameter(Mandatory = $true)]
        [string] $Target,

        [Parameter(Mandatory = $true)]
        [string] $Framework,

        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    $source = Join-Path $artifactsRoot (Join-Path ([string] $Product.id) (Join-Path $Target $Framework))
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        throw "Build payload is missing: '$source'. Run 'dev.cmd build' first or omit -SkipBuild."
    }

    Ensure-Directory -Path $Destination
    foreach ($file in @(Get-ChildItem -LiteralPath $source -File | Sort-Object Name)) {
        $extension = $file.Extension.ToLowerInvariant()
        if ($extension -ne '.dll' -and $extension -ne '.gha') {
            continue
        }
        if ($file.Name -match '^(RhinoCommon|Grasshopper)(?:\.|$)') {
            continue
        }
        if ([string] $Product.id -eq 'simple-dragon' -and
            $file.Name -eq 'GonieGonie.InvisibleDragon.GH.gha') {
            continue
        }
        if ([string] $Product.id -eq 'invisible-dragon' -and
            $file.Name -match '^GonieGonie\.SimpleDragon\.') {
            continue
        }

        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $Destination $file.Name) -Force
    }

    foreach ($required in @($Product.required_assemblies)) {
        if (-not (Test-Path -LiteralPath (Join-Path $Destination ([string] $required)) -PathType Leaf)) {
            throw "Required $($Product.display_name) assembly '$required' is missing from $Target/$Framework."
        }
    }
}

function Write-PayloadManifest {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Product,

        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Kind,

        [Parameter(Mandatory = $true)]
        [object[]] $Targets,

        [Parameter(Mandatory = $true)]
        [object] $EmbeddedPayload
    )

    $payload = foreach ($file in @(Get-ChildItem -LiteralPath $Root -File -Recurse |
        Where-Object { $_.Extension -eq '.dll' -or $_.Extension -eq '.gha' } |
        Sort-Object FullName)) {
        $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName)
        [pscustomobject] [ordered] @{
            path = Get-RelativeUnixPath -Root $Root -Path $file.FullName
            size = $file.Length
            sha256 = Get-Sha256 -Path $file.FullName
            fileVersion = $versionInfo.FileVersion
            productVersion = $versionInfo.ProductVersion
        }
    }

    $manifest = [pscustomobject] [ordered] @{
        schema = 'goniegonie.dragons-grasshopper.payload-manifest.v1'
        product = [pscustomobject] [ordered] @{
            id = [string] $Product.id
            name = [string] $Product.display_name
            version = [string] $spec.version
            owner = 'Gonie-Gonie'
        }
        kind = $Kind
        platform = 'win-x64'
        targets = @($Targets)
        runtime = [pscustomobject] [ordered] @{
            energyPlus = if ([string] $Product.id -eq 'invisible-dragon') { 'embedded-pinned-archive' } else { 'external-pinned-bootstrap-or-reuse' }
            energyPlusBinariesIncluded = ([string] $Product.id -eq 'invisible-dragon')
            weatherIncluded = ([string] $Product.id -eq 'simple-dragon')
            pythonRequired = $false
            embeddedPayloads = @($EmbeddedPayload)
        }
        payload = @($payload)
    }
    Write-Utf8JsonIfChanged -InputObject $manifest -Path (Join-Path $Root 'package-manifest.json') -Depth 10
    Write-Checksums -Root $Root
}

function New-YakInspectionHost {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Target,

        [Parameter(Mandatory = $true)]
        [string] $VerifiedYakExecutable
    )

    $targetId = [string] $Target.id
    $hostRoot = Join-Path $workingRoot (Join-Path 'yak-inspection-host' $targetId)
    Ensure-Directory -Path $hostRoot

    $hostYak = Join-Path $hostRoot 'yak.exe'
    Copy-Item -LiteralPath $VerifiedYakExecutable -Destination $hostYak -Force
    $expectedYakHash = Get-Sha256 -Path $VerifiedYakExecutable
    if ((Get-Sha256 -Path $hostYak) -ne $expectedYakHash) {
        throw "The temp Yak inspection-host copy failed SHA-256 verification for $targetId."
    }

    $sdkVersion = [string] $Target.inspection_sdk_version
    $sdkFramework = [string] $Target.inspection_sdk_framework
    if ([string]::IsNullOrWhiteSpace($sdkVersion) -or [string]::IsNullOrWhiteSpace($sdkFramework)) {
        throw "Yak inspection SDK metadata is missing for $targetId."
    }

    $packagesPath = [string] $settings.paths.nugetPackages
    foreach ($packageName in @('grasshopper', 'rhinocommon')) {
        $assetRoot = Join-Path $packagesPath (Join-Path $packageName (Join-Path $sdkVersion (Join-Path 'lib' $sdkFramework)))
        if (-not (Test-Path -LiteralPath $assetRoot -PathType Container)) {
            throw "Locked $packageName $sdkVersion/$sdkFramework assets are missing. Run 'dev.cmd setup' first."
        }

        foreach ($sdkAssembly in @(Get-ChildItem -LiteralPath $assetRoot -Filter '*.dll' -File | Sort-Object Name)) {
            Copy-Item -LiteralPath $sdkAssembly.FullName -Destination (Join-Path $hostRoot $sdkAssembly.Name) -Force
        }
    }

    foreach ($required in @('Grasshopper.dll', 'GH_IO.dll', 'RhinoCommon.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $hostRoot $required) -PathType Leaf)) {
            throw "The temp Yak inspection host for $targetId is missing '$required'."
        }
    }

    return [pscustomobject] [ordered] @{
        executable = $hostYak
        probeDirectory = $hostRoot
        yakSha256 = $expectedYakHash
        sdkVersion = $sdkVersion
        sdkFramework = $sdkFramework
    }
}

function New-YakDistribution {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Product,

        [Parameter(Mandatory = $true)]
        [object] $Target,

        [Parameter(Mandatory = $true)]
        [string] $StageRoot,

        [Parameter(Mandatory = $true)]
        [string] $YakOutputRoot,

        [Parameter(Mandatory = $true)]
        [string] $YakExecutable,

        [Parameter(Mandatory = $true)]
        [string] $StartupHook,

        [Parameter(Mandatory = $true)]
        [string[]] $ProbeDirectories
    )

    $before = @(Get-ChildItem -LiteralPath $StageRoot -Filter '*.yak' -File -ErrorAction SilentlyContinue)
    if ($before.Count -ne 0) {
        throw "Yak stage unexpectedly contains an archive before build: '$StageRoot'."
    }

    $logPath = Join-Path $workingRoot (Join-Path 'logs' ("yak-$($Product.id)-$($Target.id).log"))
    $oldStartupHooks = [Environment]::GetEnvironmentVariable('DOTNET_STARTUP_HOOKS', 'Process')
    $oldProbePaths = [Environment]::GetEnvironmentVariable('GONIEGONIE_YAK_INSPECTION_PATHS', 'Process')
    $startupHooks = if ([string]::IsNullOrWhiteSpace($oldStartupHooks)) {
        $StartupHook
    }
    else {
        $StartupHook + [System.IO.Path]::PathSeparator + $oldStartupHooks
    }
    $probePaths = @($ProbeDirectories |
        ForEach-Object { [System.IO.Path]::GetFullPath($_) } |
        Select-Object -Unique) -join [System.IO.Path]::PathSeparator

    Push-Location $StageRoot
    try {
        [Environment]::SetEnvironmentVariable('DOTNET_STARTUP_HOOKS', $startupHooks, 'Process')
        [Environment]::SetEnvironmentVariable('GONIEGONIE_YAK_INSPECTION_PATHS', $probePaths, 'Process')
        Invoke-LoggedNativeCommand `
            -FilePath $YakExecutable `
            -ArgumentList @('build', '--platform', 'win') `
            -LogPath $logPath `
            -FailureMessage "Yak build failed for $($Product.display_name) $($Target.id)"
    }
    finally {
        [Environment]::SetEnvironmentVariable('DOTNET_STARTUP_HOOKS', $oldStartupHooks, 'Process')
        [Environment]::SetEnvironmentVariable('GONIEGONIE_YAK_INSPECTION_PATHS', $oldProbePaths, 'Process')
        Pop-Location
    }

    $built = @(Get-ChildItem -LiteralPath $StageRoot -Filter '*.yak' -File)
    if ($built.Count -ne 1) {
        throw "Expected exactly one Yak output in '$StageRoot', found $($built.Count)."
    }

    $expectedMajor = if ([string] $Target.id -eq 'rhino7') { '7' } else { '8' }
    $namePrefix = [regex]::Escape(([string] $Product.id) + '-' + ([string] $spec.version))
    $rhinoPattern = '^' + $namePrefix + '-rh(?<major>[78])(?:_\d+)?-win\.yak$'
    $rhinoMatch = [regex]::Match(
        $built[0].Name,
        $rhinoPattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $rhinoMatch.Success -or $rhinoMatch.Groups['major'].Value -ne $expectedMajor) {
        throw "Yak emitted unexpected distribution filename '$($built[0].Name)' for $($Target.id)."
    }

    Ensure-Directory -Path $YakOutputRoot
    $canonicalName = '{0}-{1}-rh{2}-win.yak' -f $Product.id, $spec.version, $expectedMajor
    $destination = Join-Path $YakOutputRoot $canonicalName
    $emittedPrefix = ([string] $Product.id) + '-' + ([string] $spec.version) + '-'
    $emittedTag = $built[0].BaseName.Substring($emittedPrefix.Length)
    Move-Item -LiteralPath $built[0].FullName -Destination $destination -Force
    return [pscustomobject] [ordered] @{
        target = [string] $Target.id
        emittedFilename = $built[0].Name
        distributionTag = $emittedTag
        artifact = Get-RelativeUnixPath -Root $packagesRoot -Path $destination
        sha256 = Get-Sha256 -Path $destination
    }
}

if (-not (Test-Path -LiteralPath $specPath -PathType Leaf)) {
    throw "Package specification is missing: '$specPath'."
}
if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
    throw "Local setup is missing. Run 'dev.cmd setup' first; expected '$settingsPath'."
}
if (-not (Test-Path -LiteralPath $distributionManifestPath -PathType Leaf)) {
    throw "Distribution manifest is missing: '$distributionManifestPath'."
}

$spec = Get-Content -LiteralPath $specPath -Raw | ConvertFrom-Json
if ([string] $spec.schema -ne 'goniegonie.dragons-grasshopper.package-spec.v1') {
    throw "Unsupported package spec schema in '$specPath'."
}
$distributionManifest = Get-Content -LiteralPath $distributionManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string] $distributionManifest.schema -ne 'goniegonie.dragons-grasshopper.distributions.v1' -or
    @($distributionManifest.payloads).Count -ne 2 -or
    @($distributionManifest.payloads | Group-Object product | Where-Object { $_.Count -ne 1 }).Count -ne 0) {
    throw "Distribution manifest must define exactly one reviewed payload for each product: '$distributionManifestPath'."
}
$reviewedDistributions = @{
    'invisible-dragon' = @{
        id = 'energyplus-24.2.0-windows-x64'
        kind = 'energyplus-archive'
        fileName = 'EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip'
        url = 'https://github.com/NREL/EnergyPlus/releases/download/v24.2.0a/EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip'
        size = [int64] 179248139
        sha256 = '26c7c22b731f54031626750284c8b613fb8f03c3aa56b6bc7ec65b6bf8668df1'
        developmentPath = 'energyplus/EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip'
        packagePath = 'runtime/energyplus/EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip'
        licenseEntry = 'EnergyPlus-24.2.0-94a887817b-Windows-x86_64/LICENSE.txt'
        packageLicensePath = 'runtime/energyplus/LICENSE.txt'
        licenseSize = [int64] 3182
        licenseSha256 = 'b43f1553459a4bcc49d180b42123a64a54fcbb6213cd99ac6ac6aa32cb1c1a05'
    }
    'simple-dragon' = @{
        id = 'korean-tmy-v1'
        kind = 'weather-archive'
        fileName = 'KoreanTMY-v1.zip'
        url = 'https://github.com/snu-bslab/EPlusSimple-resources/releases/download/weather/v1/KoreanTMY-v1.zip'
        size = [int64] 128349513
        sha256 = 'fa88b8d69364b6a6b663afdc6dc2eb30c0ddee17cd37e5802ce5a5dec63d92d0'
        developmentPath = 'weather/KoreanTMY-v1.zip'
        packagePath = 'runtime/weather/KoreanTMY-v1.zip'
    }
}
foreach ($distribution in @($distributionManifest.payloads)) {
    $productId = [string] $distribution.product
    if (-not $reviewedDistributions.ContainsKey($productId)) {
        throw "Distribution manifest contains an unreviewed product '$productId'."
    }
    $expected = $reviewedDistributions[$productId]
    if ([string] $distribution.id -ne [string] $expected.id -or
        [string] $distribution.kind -ne [string] $expected.kind -or
        [string] $distribution.fileName -ne [string] $expected.fileName -or
        [string] $distribution.url -ne [string] $expected.url -or
        [int64] $distribution.size -ne [int64] $expected.size -or
        ([string] $distribution.sha256).ToLowerInvariant() -ne [string] $expected.sha256 -or
        [string] $distribution.developmentPath -ne [string] $expected.developmentPath -or
        [string] $distribution.packagePath -ne [string] $expected.packagePath -or
        ($productId -eq 'invisible-dragon' -and (
            [string] $distribution.licenseEntry -ne [string] $expected.licenseEntry -or
            [string] $distribution.packageLicensePath -ne [string] $expected.packageLicensePath -or
            [int64] $distribution.licenseSize -ne [int64] $expected.licenseSize -or
            ([string] $distribution.licenseSha256).ToLowerInvariant() -ne [string] $expected.licenseSha256))) {
        throw "Distribution pin differs from the reviewed product/path contract for '$productId'."
    }
}
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$dotnet = [string] $settings.dotnet.executable
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw "The setup-selected dotnet executable is missing: '$dotnet'."
}
Set-RepositoryBuildEnvironment -RepositoryRoot $repositoryRoot -DotNetExecutable $dotnet

if (-not $SkipBuild) {
    $buildParameters = @{
        Configuration = $Configuration
        SkipTests = $true
    }
    if ($NoRestore) {
        $buildParameters.NoRestore = $true
    }
    & (Join-Path $PSScriptRoot 'build.ps1') @buildParameters
}

Reset-GeneratedDirectory -Path $workingRoot -AllowedTopLevelName 'temp'
Reset-GeneratedDirectory -Path $packagesRoot -AllowedTopLevelName 'artifacts'

$yakExecutable = & (Join-Path $PSScriptRoot 'acquire-yak.ps1') -PassThru
if ($yakExecutable -is [array]) {
    $yakExecutable = [string] $yakExecutable[-1]
}
$yakExecutable = [string] $yakExecutable

$inspectionHookProject = Join-Path $repositoryRoot 'tools\yak-inspection-host\GonieGonie.YakInspectionHost.csproj'
$inspectionHookOutput = Join-Path $workingRoot 'yak-inspection-hook'
$inspectionHookLog = Join-Path $workingRoot 'logs\yak-inspection-hook-build.log'
$inspectionHookArguments = @(
    'build',
    $inspectionHookProject,
    '--configuration', 'Release',
    '--nologo',
    '--output', $inspectionHookOutput)
if ($NoRestore) {
    $inspectionHookArguments += '--no-restore'
}
Invoke-LoggedNativeCommand `
    -FilePath $dotnet `
    -ArgumentList $inspectionHookArguments `
    -LogPath $inspectionHookLog `
    -FailureMessage 'Yak inspection startup-hook build failed'
$inspectionHook = Join-Path $inspectionHookOutput 'GonieGonie.YakInspectionHost.dll'
if (-not (Test-Path -LiteralPath $inspectionHook -PathType Leaf)) {
    throw "Yak inspection startup hook is missing: '$inspectionHook'."
}

$yakInspectionHosts = @{}
foreach ($target in @($spec.targets)) {
    $yakInspectionHosts[[string] $target.id] = New-YakInspectionHost `
        -Target $target `
        -VerifiedYakExecutable $yakExecutable
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$indexProducts = @()
foreach ($product in @($spec.products)) {
    $sourceManifest = Join-Path $repositoryRoot (Join-Path 'packaging' (Join-Path ([string] $product.id) 'manifest.yml'))
    $manifestText = [System.IO.File]::ReadAllText($sourceManifest)
    if ($manifestText -notmatch ('(?m)^name:\s*' + [regex]::Escape([string] $product.id) + '\s*$') -or
        $manifestText -notmatch ('(?m)^version:\s*' + [regex]::Escape([string] $spec.version) + '\s*$') -or
        $manifestText -notmatch '(?m)^icon:\s*icon\.png\s*$') {
        throw "Manifest identity/version/icon mismatch: '$sourceManifest'."
    }

    $productRoot = Join-Path $packagesRoot ([string] $product.id)
    $stageOutputRoot = Join-Path $productRoot 'stage'
    $yakOutputRoot = Join-Path $productRoot 'yak'
    $portableOutputRoot = Join-Path $productRoot 'portable'
    Ensure-Directory -Path $stageOutputRoot
    Ensure-Directory -Path $yakOutputRoot
    Ensure-Directory -Path $portableOutputRoot

    $yakOutputs = @()
    foreach ($target in @($spec.targets)) {
        $stageRoot = Join-Path $stageOutputRoot ([string] $target.id)
        Copy-PackageRootFiles `
            -Product $product `
            -Destination $stageRoot `
            -PayloadDescription ("Yak stage for " + [string] $target.id)
        $stageEmbeddedPayload = Copy-EmbeddedDistribution -Product $product -Destination $stageRoot

        $targetManifest = @()
        foreach ($framework in @($target.frameworks)) {
            $frameworkName = [string] $framework
            $payloadDestination = if ([string] $target.yak_layout -eq 'flat') {
                $stageRoot
            }
            else {
                Join-Path $stageRoot $frameworkName
            }
            Copy-FrameworkPayload `
                -Product $product `
                -Target ([string] $target.id) `
                -Framework $frameworkName `
                -Destination $payloadDestination
            $targetManifest += [pscustomobject] [ordered] @{
                rhino = [string] $target.id
                framework = $frameworkName
                layout = [string] $target.yak_layout
                distribution = [string] $target.distribution_tag
            }
        }

        Write-PayloadManifest `
            -Product $product `
            -Root $stageRoot `
            -Kind 'yak-stage' `
            -Targets $targetManifest `
            -EmbeddedPayload $stageEmbeddedPayload
        $inspectionHost = $yakInspectionHosts[[string] $target.id]
        $payloadProbeDirectories = @()
        if ([string] $target.yak_layout -eq 'flat') {
            $payloadProbeDirectories += $stageRoot
        }
        else {
            foreach ($framework in @($target.frameworks)) {
                $payloadProbeDirectories += Join-Path $stageRoot ([string] $framework)
            }
        }
        $payloadProbeDirectories += [string] $inspectionHost.probeDirectory
        $yakOutputs += New-YakDistribution `
            -Product $product `
            -Target $target `
            -StageRoot $stageRoot `
            -YakOutputRoot $yakOutputRoot `
            -YakExecutable ([string] $inspectionHost.executable) `
            -StartupHook $inspectionHook `
            -ProbeDirectories $payloadProbeDirectories
    }

    $portableStage = Join-Path $workingRoot (Join-Path 'portable' ([string] $product.id))
    Copy-PackageRootFiles `
        -Product $product `
        -Destination $portableStage `
        -PayloadDescription 'portable plugin bundle'
    $portableEmbeddedPayload = Copy-EmbeddedDistribution -Product $product -Destination $portableStage
    $portableTargets = @()
    foreach ($target in @($spec.targets)) {
        foreach ($framework in @($target.frameworks)) {
            $frameworkName = [string] $framework
            $payloadDestination = Join-Path $portableStage (Join-Path ([string] $target.id) $frameworkName)
            Copy-FrameworkPayload `
                -Product $product `
                -Target ([string] $target.id) `
                -Framework $frameworkName `
                -Destination $payloadDestination
            $portableTargets += [pscustomobject] [ordered] @{
                rhino = [string] $target.id
                framework = $frameworkName
                layout = 'portable-framework-directories'
                distribution = [string] $target.distribution_tag
            }
        }
    }
    Write-PayloadManifest `
        -Product $product `
        -Root $portableStage `
        -Kind 'portable-plugin' `
        -Targets $portableTargets `
        -EmbeddedPayload $portableEmbeddedPayload

    $portableName = '{0}-{1}-portable-plugin-win.zip' -f $product.id, $spec.version
    $portablePath = Join-Path $portableOutputRoot $portableName
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $portableStage,
        $portablePath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)

    $indexProducts += [pscustomobject] [ordered] @{
        id = [string] $product.id
        name = [string] $product.display_name
        version = [string] $spec.version
        yak = @($yakOutputs)
        portable = [pscustomobject] [ordered] @{
            artifact = Get-RelativeUnixPath -Root $packagesRoot -Path $portablePath
            sha256 = Get-Sha256 -Path $portablePath
        }
        runtime = [pscustomobject] [ordered] @{
            energyPlusBinariesIncluded = ([string] $product.id -eq 'invisible-dragon')
            weatherIncluded = ([string] $product.id -eq 'simple-dragon')
            pythonRequired = $false
            embeddedPayload = $portableEmbeddedPayload
        }
    }
}

$index = [pscustomobject] [ordered] @{
    schema = 'goniegonie.dragons-grasshopper.package-index.v1'
    version = [string] $spec.version
    owner = 'Gonie-Gonie'
    products = @($indexProducts)
    redistribution = [pscustomobject] [ordered] @{
        energyPlusBinariesIncluded = $true
        weatherIncluded = $true
        portableArchivesArePluginOnly = $false
        publicPublicationAuthorized = $false
    }
}
Write-Utf8JsonIfChanged -InputObject $index -Path (Join-Path $packagesRoot 'package-index.json') -Depth 10

$reportPath = Join-Path $packagesRoot 'compatibility-report.json'
& (Join-Path $repositoryRoot 'tests\Packaging\run.ps1') `
    -PackagesRoot $packagesRoot `
    -SpecPath $specPath `
    -DotNetExecutable $dotnet `
    -ReportPath $reportPath
if ($LASTEXITCODE -ne 0) {
    throw "Package verification failed with exit code $LASTEXITCODE."
}

Write-Checksums -Root $packagesRoot
& (Join-Path $repositoryRoot 'tests\Packaging\run.ps1') `
    -PackagesRoot $packagesRoot `
    -SpecPath $specPath `
    -DotNetExecutable $dotnet
if ($LASTEXITCODE -ne 0) {
    throw "Final package checksum verification failed with exit code $LASTEXITCODE."
}

if ($RunPortableHostGate) {
    $portableHostGate = Join-Path $repositoryRoot 'tools\grasshopper-smoke\run.ps1'
    if (-not (Test-Path -LiteralPath $portableHostGate -PathType Leaf)) {
        throw "Portable Grasshopper host gate is missing: '$portableHostGate'."
    }

    Write-Host 'Running portable package host gate (Rhino 7/8 x InvisibleOnly/SimpleOnly/Both)...'
    & $portableHostGate `
        -Source 'PortablePackage' `
        -Scenario 'All' `
        -Target 'All' `
        -SkipPluginBuild `
        -PackagesRoot $packagesRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Portable package host gate failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Packaging complete: $packagesRoot"
Write-Host 'No package was published or installed.'

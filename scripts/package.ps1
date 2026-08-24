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
        Assert-NoReparsePoints -Path $safePath
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

    $readme = @"
# $($Product.display_name) $($spec.version)

This is a Gonie-Gonie $PayloadDescription for Grasshopper on Windows.

It contains only managed plugin/runtime-bootstrap assemblies. RhinoCommon,
Grasshopper, debug symbols, XML documentation, Python, EnergyPlus binaries,
and weather files are intentionally excluded. When a simulation is requested,
the Gonie-Gonie runtime bootstrap validates/reuses a compatible EnergyPlus
installation or securely prepares the pinned runtime. Supply an EPW separately.

See `package-manifest.json`, `checksums.sha256`, `LICENSE.txt`, and `NOTICE.md`
in this directory for identity, integrity, licensing, and provenance details.
"@
    Write-Utf8Text -Path (Join-Path $Destination 'README.md') -Content ($readme.Trim() + [Environment]::NewLine)
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
        throw "Build payload is missing: '$source'. Run build.cmd first or omit -SkipBuild."
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
        [object[]] $Targets
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
            energyPlus = 'external-pinned-bootstrap-or-reuse'
            energyPlusBinariesIncluded = $false
            weatherIncluded = $false
            pythonRequired = $false
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
            throw "Locked $packageName $sdkVersion/$sdkFramework assets are missing. Run setup.cmd/restore first."
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
    throw "Local setup is missing. Run setup.cmd first; expected '$settingsPath'."
}

$spec = Get-Content -LiteralPath $specPath -Raw | ConvertFrom-Json
if ([string] $spec.schema -ne 'goniegonie.dragons-grasshopper.package-spec.v1') {
    throw "Unsupported package spec schema in '$specPath'."
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
    & (Join-Path $repositoryRoot 'build.ps1') @buildParameters
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
            -Targets $targetManifest
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
        -Targets $portableTargets

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
    }
}

$index = [pscustomobject] [ordered] @{
    schema = 'goniegonie.dragons-grasshopper.package-index.v1'
    version = [string] $spec.version
    owner = 'Gonie-Gonie'
    products = @($indexProducts)
    redistribution = [pscustomobject] [ordered] @{
        energyPlusBinariesIncluded = $false
        weatherIncluded = $false
        portableArchivesArePluginOnly = $true
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

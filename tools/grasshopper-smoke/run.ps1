#requires -Version 5.1

[CmdletBinding()]
param(
    [ValidateSet("All", "Rhino8", "Rhino7")]
    [Alias("Host")]
    [string]$Target = "All",

    [ValidateSet("Both", "InvisibleOnly", "SimpleOnly", "All")]
    [string]$Scenario = "Both",

    [ValidateSet("BuildOutput", "PortablePackage")]
    [string]$Source = "BuildOutput",

    [string]$PackagesRoot,

    [string]$Rhino8Exe = "C:\Program Files\Rhino 8\System\Rhino.exe",

    [string]$Rhino7Exe = "C:\Program Files\Rhino 7\System\Rhino.exe",

    [ValidateRange(15, 600)]
    [int]$TimeoutSeconds = 60,

    [switch]$SkipPluginBuild,

    [switch]$ArchiveSafetySelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$toolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [IO.Path]::GetFullPath((Join-Path $toolRoot "..\.."))
if ([string]::IsNullOrWhiteSpace($PackagesRoot)) {
    $PackagesRoot = Join-Path $repoRoot "artifacts\packages"
}
$PackagesRoot = [IO.Path]::GetFullPath($PackagesRoot)
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss-fff")
$runRoot = Join-Path $repoRoot "temp\grasshopper-smoke\run-$runStamp"
[IO.Directory]::CreateDirectory($runRoot) | Out-Null

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Test-PathWithin([string]$Root, [string]$Candidate) {
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $candidateFull = [IO.Path]::GetFullPath($Candidate)
    if ($candidateFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    return $candidateFull.StartsWith(
        $rootFull + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparseAncestors([string]$Root, [string]$Candidate) {
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $current = [IO.Path]::GetFullPath($Candidate).TrimEnd('\', '/')
    if (-not (Test-PathWithin $rootFull $current)) {
        throw "Path '$current' is outside the permitted root '$rootFull'."
    }

    while ($true) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse points are not permitted in the host-gate path: '$current'."
            }
        }

        if ($current.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
            break
        }

        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent.Equals($current, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Could not reach permitted root '$rootFull' from '$current'."
        }
        $current = $parent.TrimEnd('\', '/')
    }
}

function Assert-NoReparseChain([string]$Path) {
    $current = Get-Item -LiteralPath ([IO.Path]::GetFullPath($Path)) -Force
    while ($null -ne $current) {
        if (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Reparse points are not permitted in the host-gate path: '$($current.FullName)'."
        }

        if ($current -is [IO.FileInfo]) {
            $current = $current.Directory
        }
        else {
            $current = $current.Parent
        }
    }
}

function Resolve-DotNet {
    $local = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"
    if (Test-Path -LiteralPath $local -PathType Leaf) {
        return $local
    }

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "dotnet was not found. Run setup.cmd before the Grasshopper host gate."
    }

    return $command.Source
}

function Require-File([string]$Path, [string]$Label) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw "$Label was not found: $full"
    }

    return $full
}

function Require-Directory([string]$Path, [string]$Label) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $full -PathType Container)) {
        throw "$Label was not found: $full"
    }

    return $full
}

function Invoke-DotNetLogged([string[]]$Arguments, [string]$LogName) {
    $logPath = Join-Path $runRoot $LogName
    & $script:dotnet @Arguments 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE. See $logPath"
    }
}

function ConvertTo-ProcessArguments([string[]]$Arguments) {
    return ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + $_.Replace('"', '\"') + '"'
        }
        else {
            $_
        }
    }) -join ' '
}

function Invoke-BoundedHost(
    [string]$FilePath,
    [string[]]$Arguments,
    [string]$OutputDirectory,
    [hashtable]$EnvironmentVariables) {
    [IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
    $stdoutPath = Join-Path $OutputDirectory "stdout.log"
    $stderrPath = Join-Path $OutputDirectory "stderr.log"

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = ConvertTo-ProcessArguments $Arguments
    $startInfo.WorkingDirectory = $OutputDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($entry in $EnvironmentVariables.GetEnumerator()) {
        $startInfo.EnvironmentVariables[$entry.Key] = [string]$entry.Value
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Host process did not start: $FilePath"
    }

    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try {
            $process.Kill()
            $process.WaitForExit()
        }
        finally {
            [IO.File]::WriteAllText($stdoutPath, $stdoutTask.Result)
            [IO.File]::WriteAllText($stderrPath, $stderrTask.Result)
            $process.Dispose()
        }

        throw "Host process exceeded the $TimeoutSeconds-second limit and was terminated. See $OutputDirectory"
    }

    $process.WaitForExit()
    $exitCode = $process.ExitCode
    [IO.File]::WriteAllText($stdoutPath, $stdoutTask.Result)
    [IO.File]::WriteAllText($stderrPath, $stderrTask.Result)
    $process.Dispose()

    Get-Content -LiteralPath $stdoutPath
    if ($exitCode -ne 0) {
        Get-Content -LiteralPath $stderrPath
        throw "Host process failed with exit code $exitCode. See $OutputDirectory"
    }
}

function Build-Plugin([string]$Project, [string]$Framework, [string]$Label) {
    if ($SkipPluginBuild -or $Source -eq "PortablePackage") {
        return
    }

    Invoke-DotNetLogged @(
        "build", $Project,
        "--configuration", "Release",
        "--framework", $Framework,
        "--no-restore",
        "--nologo"
    ) "build-$Label.log"
}

function Get-RequestedScenarios {
    if ($Scenario -eq "All") {
        return @("InvisibleOnly", "SimpleOnly", "Both")
    }

    return @($Scenario)
}

function Get-ScenarioProducts([string]$ScenarioName) {
    switch ($ScenarioName) {
        "InvisibleOnly" { return @("invisible-dragon") }
        "SimpleOnly" { return @("simple-dragon") }
        "Both" { return @("invisible-dragon", "simple-dragon") }
        default { throw "Unsupported scenario '$ScenarioName'." }
    }
}

function Get-ProductGhaName([string]$ProductId) {
    switch ($ProductId) {
        "invisible-dragon" { return "GonieGonie.InvisibleDragon.GH.gha" }
        "simple-dragon" { return "GonieGonie.SimpleDragon.GH.gha" }
        default { throw "Unsupported product '$ProductId'." }
    }
}

function Get-ProductTypesName([string]$ProductId) {
    switch ($ProductId) {
        "invisible-dragon" { return "GonieGonie.InvisibleDragon.Grasshopper.Types.dll" }
        "simple-dragon" { return "GonieGonie.SimpleDragon.Grasshopper.Types.dll" }
        default { throw "Unsupported product '$ProductId'." }
    }
}

function Get-ProductDisplayName([string]$ProductId) {
    switch ($ProductId) {
        "invisible-dragon" { return "InvisibleDragon" }
        "simple-dragon" { return "SimpleDragon" }
        default { throw "Unsupported product '$ProductId'." }
    }
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Read-PackageIndex {
    $indexPath = Require-File (
        Join-Path $PackagesRoot "package-index.json"
    ) "portable package index"
    Assert-NoReparseChain $indexPath
    $index = Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
    if ([string]$index.schema -ne "goniegonie.dragons-grasshopper.package-index.v1") {
        throw "Unsupported portable package index schema in '$indexPath'."
    }

    return $index
}

function Resolve-PortableArchive([string]$ProductId, [object]$PackageIndex) {
    $products = @($PackageIndex.products | Where-Object { [string]$_.id -eq $ProductId })
    if ($products.Count -ne 1) {
        throw "Package index must contain exactly one '$ProductId' product; found $($products.Count)."
    }

    $artifact = [string]$products[0].portable.artifact
    $expectedSha256 = [string]$products[0].portable.sha256
    if ([string]::IsNullOrWhiteSpace($artifact) -or
        $artifact.Contains('\') -or
        $artifact.StartsWith('/') -or
        $artifact -match '^[A-Za-z]:' -or
        @($artifact.Split('/') | Where-Object { $_ -eq '.' -or $_ -eq '..' -or [string]::IsNullOrWhiteSpace($_) }).Count -ne 0) {
        throw "Package index contains an unsafe/non-canonical portable artifact path for '$ProductId'."
    }
    if ($expectedSha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw "Package index contains an invalid portable SHA-256 for '$ProductId'."
    }

    $indexedArchive = [IO.Path]::GetFullPath((
        Join-Path $PackagesRoot $artifact.Replace('/', [IO.Path]::DirectorySeparatorChar)))
    if (-not (Test-PathWithin $PackagesRoot $indexedArchive)) {
        throw "Indexed portable artifact escapes the package root: '$artifact'."
    }

    $portableDirectory = Require-Directory (
        Join-Path $PackagesRoot (Join-Path $ProductId "portable")
    ) "$ProductId portable artifact directory"
    Assert-NoReparseAncestors -Root $PackagesRoot -Candidate $portableDirectory
    Assert-NoReparseChain $portableDirectory
    $archives = @(Get-ChildItem -LiteralPath $portableDirectory -File |
        Where-Object { $_.Name -like "$ProductId-*-portable-plugin-win.zip" })
    if ($archives.Count -ne 1) {
        throw "Expected exactly one $ProductId portable plugin ZIP in '$portableDirectory'; found $($archives.Count)."
    }

    Assert-NoReparseAncestors -Root $PackagesRoot -Candidate $archives[0].FullName
    Assert-NoReparseChain $archives[0].FullName
    if (-not $archives[0].FullName.Equals($indexedArchive, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The sole portable archive does not match package-index artifact '$artifact'."
    }

    $actualSha256 = Get-Sha256 $archives[0].FullName
    if (-not $actualSha256.Equals($expectedSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Portable archive SHA-256 mismatch for '$ProductId': expected $expectedSha256, got $actualSha256."
    }

    return [pscustomobject]@{
        ProductId = $ProductId
        ArchivePath = $archives[0].FullName
        ArchiveSha256 = $actualSha256
        IndexedArtifact = $artifact
    }
}

function Expand-PortableArchiveSafe(
    [string]$ArchivePath,
    [string]$DestinationRoot) {
    if (Test-Path -LiteralPath $DestinationRoot) {
        throw "Portable extraction destination already exists: '$DestinationRoot'."
    }

    [IO.Directory]::CreateDirectory($DestinationRoot) | Out-Null
    Assert-NoReparseAncestors -Root $repoRoot -Candidate $DestinationRoot
    Assert-NoReparseChain $DestinationRoot
    $destinationFull = [IO.Path]::GetFullPath($DestinationRoot).TrimEnd('\', '/')
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [int64]$declaredLength = 0
    [int64]$maximumExtractedLength = 1073741824
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        foreach ($entry in $archive.Entries) {
            $entryName = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($entryName)) {
                throw "Portable ZIP contains an empty path."
            }
            $normalizedName = $entryName.Replace('\', '/')
            if ($normalizedName.StartsWith('/') -or
                $normalizedName.StartsWith('//') -or
                $normalizedName -match '^[A-Za-z]:' -or
                [IO.Path]::IsPathRooted($entryName)) {
                throw "Portable ZIP contains a rooted/device path: '$entryName'."
            }

            $isDirectory = $normalizedName.EndsWith('/')
            $logicalName = $normalizedName.TrimEnd('/')
            if ([string]::IsNullOrWhiteSpace($logicalName)) {
                throw "Portable ZIP contains an invalid root entry: '$entryName'."
            }
            $segments = $logicalName.Split('/')
            foreach ($segment in $segments) {
                if ([string]::IsNullOrWhiteSpace($segment) -or
                    $segment -eq '.' -or
                    $segment -eq '..' -or
                    $segment.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
                    throw "Portable ZIP contains an unsafe path segment in '$entryName'."
                }

                $windowsName = $segment.TrimEnd([char[]]@(' ', '.'))
                if (-not $windowsName.Equals($segment, [StringComparison]::Ordinal)) {
                    throw "Portable ZIP contains a Windows trailing-dot/space path alias in '$entryName'."
                }

                $deviceBaseName = $windowsName.Split('.')[0].TrimEnd([char[]]@(' ', '.'))
                if ($deviceBaseName -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$') {
                    throw "Portable ZIP contains a reserved Windows DOS device path in '$entryName'."
                }
            }

            if (-not $seen.Add($logicalName)) {
                throw "Portable ZIP contains a duplicate or case-ambiguous path: '$entryName'."
            }

            $unixMode = (($entry.ExternalAttributes -shr 16) -band 0xF000)
            $dosAttributes = ($entry.ExternalAttributes -band 0xFFFF)
            if ($unixMode -eq 0xA000 -or
                ($dosAttributes -band [int][IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Portable ZIP contains a link/reparse entry: '$entryName'."
            }

            $relativeWindows = $logicalName.Replace('/', [IO.Path]::DirectorySeparatorChar)
            $destination = [IO.Path]::GetFullPath((Join-Path $destinationFull $relativeWindows))
            if (-not (Test-PathWithin $destinationFull $destination)) {
                throw "Portable ZIP entry escapes its extraction root: '$entryName'."
            }

            if ($isDirectory) {
                [IO.Directory]::CreateDirectory($destination) | Out-Null
                continue
            }

            $declaredLength += [int64]$entry.Length
            if ($declaredLength -gt $maximumExtractedLength) {
                throw "Portable ZIP declares more than 1 GiB of extracted content."
            }

            $parent = Split-Path -Parent $destination
            [IO.Directory]::CreateDirectory($parent) | Out-Null
            $input = $entry.Open()
            try {
                $output = [IO.FileStream]::new(
                    $destination,
                    [IO.FileMode]::CreateNew,
                    [IO.FileAccess]::Write,
                    [IO.FileShare]::None)
                try {
                    $input.CopyTo($output)
                }
                finally {
                    $output.Dispose()
                }
            }
            finally {
                $input.Dispose()
            }
        }
    }
    catch {
        throw "Safe extraction rejected '$ArchivePath': $($_.Exception.Message)"
    }
    finally {
        $archive.Dispose()
    }

    foreach ($item in @(Get-ChildItem -LiteralPath $DestinationRoot -Force -Recurse)) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Portable ZIP extraction produced a reparse point: '$($item.FullName)'."
        }
    }
}

function New-ArchiveSafetyTestZip([string]$Path, [string[]]$EntryNames) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    $archive = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entryName in $EntryNames) {
            $entry = $archive.CreateEntry($entryName)
            $stream = $entry.Open()
            try {
                $bytes = [Text.Encoding]::UTF8.GetBytes("archive-safety-negative")
                $stream.Write($bytes, 0, $bytes.Length)
            }
            finally {
                $stream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-ArchiveEntryRejected(
    [string]$CaseName,
    [string[]]$EntryNames,
    [string]$ExpectedMessage) {
    $caseRoot = Join-Path $runRoot ("archive-safety-self-test\" + $CaseName)
    $archivePath = Join-Path $caseRoot "source.zip"
    New-ArchiveSafetyTestZip $archivePath $EntryNames
    try {
        Expand-PortableArchiveSafe $archivePath (Join-Path $caseRoot "extracted")
    }
    catch {
        if ($_.Exception.Message -notmatch [regex]::Escape($ExpectedMessage)) {
            throw "Archive negative test '$CaseName' produced the wrong failure: $($_.Exception.Message)"
        }

        Write-Host "Archive negative test passed: $CaseName"
        return
    }

    throw "Archive negative test '$CaseName' unexpectedly extracted successfully."
}

function Assert-IndexedArchiveShaRejected {
    $caseRoot = Join-Path $runRoot "archive-safety-self-test\sha-mismatch"
    $portable = Join-Path $caseRoot "invisible-dragon\portable"
    $archivePath = Join-Path $portable "invisible-dragon-0.1.0-portable-plugin-win.zip"
    New-ArchiveSafetyTestZip $archivePath @("safe.txt")
    $index = [pscustomobject]@{
        schema = "goniegonie.dragons-grasshopper.package-index.v1"
        products = @([pscustomobject]@{
            id = "invisible-dragon"
            portable = [pscustomobject]@{
                artifact = "invisible-dragon/portable/invisible-dragon-0.1.0-portable-plugin-win.zip"
                sha256 = "0" * 64
            }
        })
    }

    $originalPackagesRoot = $script:PackagesRoot
    try {
        $script:PackagesRoot = $caseRoot
        try {
            Resolve-PortableArchive "invisible-dragon" $index | Out-Null
        }
        catch {
            if ($_.Exception.Message -notmatch "Portable archive SHA-256 mismatch") {
                throw "Archive SHA negative test produced the wrong failure: $($_.Exception.Message)"
            }

            Write-Host "Archive negative test passed: package-index SHA mismatch"
            return
        }
    }
    finally {
        $script:PackagesRoot = $originalPackagesRoot
    }

    throw "Package-index SHA mismatch unexpectedly passed."
}

function Invoke-ArchiveSafetySelfTests {
    $dotDot = ([string][char]46) + ([string][char]46)
    Assert-ArchiveEntryRejected "traversal" @($dotDot + "/escape.txt") "unsafe path segment"
    Assert-ArchiveEntryRejected "trailing-dot" @("folder/alias.") "Windows trailing-dot/space path alias"
    Assert-ArchiveEntryRejected "trailing-space" @("folder/alias. ") "Windows trailing-dot/space path alias"
    Assert-ArchiveEntryRejected "reserved-nul-extension" @("folder/NUL.txt") "reserved Windows DOS device path"
    Assert-ArchiveEntryRejected "reserved-com9-extension" @("folder/com9.cfg") "reserved Windows DOS device path"
    Assert-ArchiveEntryRejected "case-ambiguous" @("folder/item.txt", "folder/ITEM.txt") "duplicate or case-ambiguous path"
    Assert-IndexedArchiveShaRejected
}

function Resolve-PortablePayload(
    [string]$ProductId,
    [string]$ExtractedRoot,
    [string]$TargetId,
    [string]$Framework,
    [object]$ArchiveProvenance) {
    $payloadRoot = Require-Directory (
        Join-Path $ExtractedRoot (Join-Path $TargetId $Framework)
    ) "$ProductId $TargetId/$Framework payload"
    Assert-NoReparseAncestors -Root $ExtractedRoot -Candidate $payloadRoot

    $expectedGhaName = Get-ProductGhaName $ProductId
    $expectedTypesName = Get-ProductTypesName $ProductId
    $payloadGhas = @(Get-ChildItem -LiteralPath $payloadRoot -File -Filter '*.gha')
    if ($payloadGhas.Count -ne 1 -or
        -not $payloadGhas[0].Name.Equals($expectedGhaName, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$ProductId $TargetId/$Framework must contain exactly its own '$expectedGhaName' GHA."
    }

    $oppositeGha = if ($ProductId -eq "simple-dragon") {
        "GonieGonie.InvisibleDragon.GH.gha"
    }
    else {
        "GonieGonie.SimpleDragon.GH.gha"
    }
    if (@(Get-ChildItem -LiteralPath $ExtractedRoot -File -Recurse |
            Where-Object { $_.Name.Equals($oppositeGha, [StringComparison]::OrdinalIgnoreCase) }).Count -ne 0) {
        throw "$ProductId portable package illegally contains the other product GHA '$oppositeGha'."
    }

    $typesPath = Require-File (Join-Path $payloadRoot $expectedTypesName) "$ProductId Types assembly"
    return [pscustomobject]@{
        ProductId = $ProductId
        PluginPath = $payloadGhas[0].FullName
        PluginSha256 = Get-Sha256 $payloadGhas[0].FullName
        PayloadRoot = $payloadRoot
        TypesPath = $typesPath
        ArchivePath = $ArchiveProvenance.ArchivePath
        ArchiveSha256 = $ArchiveProvenance.ArchiveSha256
    }
}

function Resolve-BuildPayload([string]$ProductId, [string]$Framework) {
    $assemblyName = if ($ProductId -eq "invisible-dragon") {
        "GonieGonie.InvisibleDragon.GH"
    }
    else {
        "GonieGonie.SimpleDragon.GH"
    }
    $payloadRoot = Join-Path $repoRoot (
        "temp\build\bin\$assemblyName\Release\$Framework")
    $pluginPath = Require-File (
        Join-Path $payloadRoot (Get-ProductGhaName $ProductId)
    ) "$ProductId $Framework GHA"
    Require-File (
        Join-Path $payloadRoot (Get-ProductTypesName $ProductId)
    ) "$ProductId $Framework Types assembly" | Out-Null
    return [pscustomobject]@{
        ProductId = $ProductId
        PluginPath = $pluginPath
        PluginSha256 = Get-Sha256 $pluginPath
        PayloadRoot = [IO.Path]::GetFullPath($payloadRoot)
        ArchivePath = $null
        ArchiveSha256 = $null
    }
}

function Get-Rhino8PluginFramework([string]$Executable) {
    $versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($Executable)
    if ($versionInfo.FileMajorPart -ne 8) {
        throw "Rhino 8 executable has unexpected file version '$($versionInfo.FileVersion)'."
    }

    if ($versionInfo.FileMinorPart -ge 20) {
        return "net8.0"
    }

    return "net7.0"
}

function Assert-HostSummary(
    [string]$SummaryPath,
    [string]$ScenarioName,
    [string]$ExpectedSource,
    [object[]]$Payloads) {
    Require-File $SummaryPath "host summary" | Out-Null
    $summary = Get-Content -LiteralPath $SummaryPath -Raw | ConvertFrom-Json
    if ([string]$summary.schema -ne "goniegonie.dragons-grasshopper.host-smoke.v3") {
        throw "Host summary has an unexpected schema: '$($summary.schema)'."
    }
    if ([string]$summary.scenario -ne $ScenarioName -or
        [string]$summary.source -ne $ExpectedSource) {
        throw "Host summary scenario/source mismatch: '$($summary.scenario)'/'$($summary.source)'."
    }
    if ([int]$summary.pluginCount -ne $Payloads.Count) {
        throw "Host summary plugin count mismatch."
    }

    $expectedPaths = @($Payloads | ForEach-Object { [IO.Path]::GetFullPath($_.PluginPath) } | Sort-Object)
    $actualPaths = @($summary.pluginPaths | ForEach-Object { [IO.Path]::GetFullPath([string]$_) } | Sort-Object)
    if ($actualPaths.Count -ne $expectedPaths.Count -or
        @(Compare-Object -ReferenceObject $expectedPaths -DifferenceObject $actualPaths).Count -ne 0) {
        throw "Host summary plugin paths do not exactly match the requested payload."
    }

    $pluginArtifacts = @($summary.pluginArtifacts)
    if ($pluginArtifacts.Count -ne $Payloads.Count) {
        throw "Host summary must record one hashed plugin artifact per requested GHA."
    }
    foreach ($payload in $Payloads) {
        $product = Get-ProductDisplayName ([string]$payload.ProductId)
        if (-not (Get-Sha256 $payload.PluginPath).Equals(
                [string]$payload.PluginSha256,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "GHA changed before summary verification for '$product'."
        }
        $matches = @($pluginArtifacts | Where-Object { [string]$_.product -eq $product })
        if ($matches.Count -ne 1 -or
            -not ([IO.Path]::GetFullPath([string]$matches[0].path)).Equals(
                [IO.Path]::GetFullPath([string]$payload.PluginPath),
                [StringComparison]::OrdinalIgnoreCase) -or
            -not ([string]$matches[0].sha256).Equals(
                [string]$payload.PluginSha256,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Host summary plugin provenance mismatch for '$product'."
        }
    }

    $portableArchives = @($summary.portableArchives)
    if ($ExpectedSource -eq "portable-package") {
        if ($portableArchives.Count -ne $Payloads.Count) {
            throw "Portable host summary must record one source archive per requested product."
        }
        foreach ($payload in $Payloads) {
            $product = Get-ProductDisplayName ([string]$payload.ProductId)
            if (-not (Get-Sha256 $payload.ArchivePath).Equals(
                    [string]$payload.ArchiveSha256,
                    [StringComparison]::OrdinalIgnoreCase)) {
                throw "Portable archive changed before summary verification for '$product'."
            }
            $matches = @($portableArchives | Where-Object { [string]$_.product -eq $product })
            if ($matches.Count -ne 1 -or
                -not ([IO.Path]::GetFullPath([string]$matches[0].path)).Equals(
                    [IO.Path]::GetFullPath([string]$payload.ArchivePath),
                    [StringComparison]::OrdinalIgnoreCase) -or
                -not ([string]$matches[0].sha256).Equals(
                    [string]$payload.ArchiveSha256,
                    [StringComparison]::OrdinalIgnoreCase)) {
                throw "Host summary portable archive provenance mismatch for '$product'."
            }
        }
    }
    elseif ($portableArchives.Count -ne 0) {
        throw "Build-output host summary must not claim portable archive provenance."
    }

    $expectsInvisible = $ScenarioName -ne "SimpleOnly"
    $expectsSimple = $ScenarioName -ne "InvisibleOnly"
    if (($expectsInvisible -and ([int]$summary.registeredInvisibleComponents -le 0 -or
            [int]$summary.registeredInvisibleParameters -le 0)) -or
        (-not $expectsInvisible -and ([int]$summary.registeredInvisibleComponents -ne 0 -or
            [int]$summary.registeredInvisibleParameters -ne 0))) {
        throw "Host summary InvisibleDragon discovery counts do not match '$ScenarioName'."
    }
    if (($expectsSimple -and ([int]$summary.registeredSimpleComponents -le 0 -or
            [int]$summary.registeredSimpleParameters -le 0)) -or
        (-not $expectsSimple -and ([int]$summary.registeredSimpleComponents -ne 0 -or
            [int]$summary.registeredSimpleParameters -ne 0))) {
        throw "Host summary SimpleDragon discovery counts do not match '$ScenarioName'."
    }

    $expectedPersistenceCount = if ($ScenarioName -eq "Both") { 2 } else { 1 }
    if (@($summary.persistence).Count -ne $expectedPersistenceCount) {
        throw "Host summary persistence count does not match '$ScenarioName'."
    }
}

function Invoke-ScenarioHost(
    [string]$HostName,
    [string]$Runner,
    [string[]]$RunnerArguments,
    [string]$RhinoExecutable,
    [string]$ScenarioName,
    [object[]]$Payloads) {
    $scenarioOutput = Join-Path $runRoot (
        $HostName.ToLowerInvariant() + "\" + $ScenarioName.ToLowerInvariant())
    $documentPath = Join-Path $scenarioOutput "dragons-$($ScenarioName.ToLowerInvariant())-host-gate.gh"
    $pluginPaths = @($Payloads | ForEach-Object { $_.PluginPath })
    $pluginHashes = @($Payloads | ForEach-Object {
        $actualHash = Get-Sha256 $_.PluginPath
        if (-not $actualHash.Equals([string]$_.PluginSha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "GHA changed after payload resolution: '$($_.PluginPath)'."
        }
        $actualHash
    })
    $archives = @($Payloads | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.ArchivePath) })
    foreach ($archive in $archives) {
        $actualHash = Get-Sha256 $archive.ArchivePath
        if (-not $actualHash.Equals([string]$archive.ArchiveSha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Portable archive changed after extraction: '$($archive.ArchivePath)'."
        }
    }
    $payloadRoots = @($Payloads | ForEach-Object { $_.PayloadRoot } |
        Select-Object -Unique)
    $environment = @{
        DRAGONS_SMOKE_SCENARIO = $ScenarioName
        DRAGONS_SMOKE_SOURCE = if ($Source -eq "PortablePackage") { "portable-package" } else { "build-output" }
        DRAGONS_PLUGIN_PATHS = $pluginPaths -join [IO.Path]::PathSeparator
        DRAGONS_PLUGIN_SHA256 = $pluginHashes -join [IO.Path]::PathSeparator
        DRAGONS_PORTABLE_ARCHIVE_PATHS = @($archives | ForEach-Object { $_.ArchivePath }) -join [IO.Path]::PathSeparator
        DRAGONS_PORTABLE_ARCHIVE_SHA256 = @($archives | ForEach-Object { $_.ArchiveSha256 }) -join [IO.Path]::PathSeparator
        DRAGONS_ALLOWED_PLUGIN_ROOTS = $payloadRoots -join [IO.Path]::PathSeparator
        DRAGONS_GRASSHOPPER_SMOKE_OUTPUT = $scenarioOutput
        DRAGONS_GRASSHOPPER_SMOKE_DOCUMENT = $documentPath
    }
    if ($HostName -eq "Rhino8") {
        $environment.DRAGONS_RHINO8_EXE = $RhinoExecutable
    }
    else {
        $environment.DRAGONS_RHINO7_EXE = $RhinoExecutable
    }

    Write-Host "Running $HostName $ScenarioName from $Source ($($pluginPaths.Count) GHA(s))..."
    Invoke-BoundedHost $Runner $RunnerArguments $scenarioOutput $environment
    $expectedSource = if ($Source -eq "PortablePackage") { "portable-package" } else { "build-output" }
    Assert-HostSummary (
        Join-Path $scenarioOutput "summary.json"
    ) $ScenarioName $expectedSource $Payloads
}

if ($ArchiveSafetySelfTest) {
    try {
        Invoke-ArchiveSafetySelfTests
        [IO.File]::WriteAllText(
            (Join-Path $runRoot "PASS.txt"),
            "Portable archive safety negative tests passed." + [Environment]::NewLine)
        Write-Host "Portable archive safety negative tests passed. Results: $runRoot"
        exit 0
    }
    catch {
        [IO.File]::WriteAllText((Join-Path $runRoot "FAIL.txt"), $_.Exception.ToString())
        Write-Error "Portable archive safety negative tests failed: $($_.Exception.Message)"
        exit 1
    }
}

try {
    Assert-NoReparseAncestors -Root $repoRoot -Candidate $runRoot
    $script:dotnet = Resolve-DotNet
    $scenarios = @(Get-RequestedScenarios)
    $requestedProducts = @($scenarios |
        ForEach-Object { Get-ScenarioProducts $_ } |
        Select-Object -Unique)
    $invisibleProject = Join-Path $repoRoot "src\InvisibleDragon\GonieGonie.InvisibleDragon.GH\GonieGonie.InvisibleDragon.GH.csproj"
    $simpleProject = Join-Path $repoRoot "src\SimpleDragon\GonieGonie.SimpleDragon.GH\GonieGonie.SimpleDragon.GH.csproj"

    $portableRoots = @{}
    if ($Source -eq "PortablePackage") {
        Require-Directory $PackagesRoot "package artifacts root" | Out-Null
        Assert-NoReparseChain $PackagesRoot
        $packageIndex = Read-PackageIndex
        foreach ($productId in $requestedProducts) {
            $archive = Resolve-PortableArchive $productId $packageIndex
            $extractRoot = Join-Path $runRoot ("portable-extract\" + $productId)
            Expand-PortableArchiveSafe $archive.ArchivePath $extractRoot
            $portableRoots[$productId] = [pscustomobject]@{
                ExtractedRoot = $extractRoot
                Archive = $archive
            }
        }
    }

    if ($Target -in @("All", "Rhino8")) {
        $rhino8Exe = Require-File $Rhino8Exe "Rhino 8 executable"
        $rhino8Framework = Get-Rhino8PluginFramework $rhino8Exe
        foreach ($productId in $requestedProducts) {
            $project = if ($productId -eq "invisible-dragon") { $invisibleProject } else { $simpleProject }
            Build-Plugin $project ($rhino8Framework + "-windows") "$productId-rhino8"
        }

        $rhino8Project = Join-Path $toolRoot "Rhino8\GonieGonie.Dragons.Grasshopper.Rhino8Smoke.csproj"
        $rhino8HostOutput = Join-Path $runRoot "host-runner\rhino8"
        Invoke-DotNetLogged @("restore", $rhino8Project, "--locked-mode", "--nologo") "restore-rhino8-host.log"
        Invoke-DotNetLogged @(
            "build", $rhino8Project,
            "--configuration", "Release",
            "--no-restore", "--nologo",
            "--output", $rhino8HostOutput
        ) "build-rhino8-host.log"
        $rhino8Runner = Require-File (
            Join-Path $rhino8HostOutput "GonieGonie.Dragons.Grasshopper.Rhino8Smoke.dll"
        ) "Rhino 8 host runner"

        foreach ($scenarioName in $scenarios) {
            $payloads = @(foreach ($productId in @(Get-ScenarioProducts $scenarioName)) {
                if ($Source -eq "PortablePackage") {
                    Resolve-PortablePayload `
                        $productId `
                        $portableRoots[$productId].ExtractedRoot `
                        "rhino8" `
                        $rhino8Framework `
                        $portableRoots[$productId].Archive
                }
                else {
                    Resolve-BuildPayload $productId ($rhino8Framework + "-windows")
                }
            })
            Invoke-ScenarioHost `
                -HostName "Rhino8" `
                -Runner $script:dotnet `
                -RunnerArguments @($rhino8Runner) `
                -RhinoExecutable $rhino8Exe `
                -ScenarioName $scenarioName `
                -Payloads $payloads
        }
    }

    if ($Target -in @("All", "Rhino7")) {
        $rhino7Exe = Require-File $Rhino7Exe "Rhino 7 executable"
        $rhino7System = Split-Path -Parent $rhino7Exe
        $rhino7Grasshopper = [IO.Path]::GetFullPath((Join-Path $rhino7System "..\Plug-ins\Grasshopper"))
        Require-File (Join-Path $rhino7Grasshopper "Grasshopper.dll") "Rhino 7 Grasshopper" | Out-Null
        foreach ($productId in $requestedProducts) {
            $project = if ($productId -eq "invisible-dragon") { $invisibleProject } else { $simpleProject }
            Build-Plugin $project "net48" "$productId-rhino7"
        }

        $rhino7Project = Join-Path $toolRoot "Rhino7Probe\Rhino7Probe.csproj"
        $rhino7HostOutput = Join-Path $runRoot "host-runner\rhino7"
        $rhino7Properties = @(
            "-p:Rhino7SystemDir=$rhino7System",
            "-p:Rhino7GrasshopperDir=$rhino7Grasshopper"
        )
        Invoke-DotNetLogged (@("restore", $rhino7Project, "--locked-mode", "--nologo") + $rhino7Properties) "restore-rhino7-host.log"
        Invoke-DotNetLogged (@(
            "build", $rhino7Project,
            "--configuration", "Release",
            "--no-restore", "--nologo",
            "--output", $rhino7HostOutput
        ) + $rhino7Properties) "build-rhino7-host.log"
        $rhino7Runner = Require-File (
            Join-Path $rhino7HostOutput "GonieGonie.Dragons.Grasshopper.Rhino7Probe.exe"
        ) "Rhino 7 host runner"

        foreach ($scenarioName in $scenarios) {
            $payloads = @(foreach ($productId in @(Get-ScenarioProducts $scenarioName)) {
                if ($Source -eq "PortablePackage") {
                    Resolve-PortablePayload `
                        $productId `
                        $portableRoots[$productId].ExtractedRoot `
                        "rhino7" `
                        "net48" `
                        $portableRoots[$productId].Archive
                }
                else {
                    Resolve-BuildPayload $productId "net48"
                }
            })
            Invoke-ScenarioHost `
                -HostName "Rhino7" `
                -Runner $rhino7Runner `
                -RunnerArguments @() `
                -RhinoExecutable $rhino7Exe `
                -ScenarioName $scenarioName `
                -Payloads $payloads
        }
    }

    [IO.File]::WriteAllText(
        (Join-Path $runRoot "PASS.txt"),
        "Grasshopper host gate passed for $Target/$Scenario/$Source at $([DateTimeOffset]::UtcNow.ToString('O'))." + [Environment]::NewLine)
    Write-Host "Grasshopper host gate passed. Logs and documents: $runRoot"
}
catch {
    [IO.File]::WriteAllText((Join-Path $runRoot "FAIL.txt"), $_.Exception.ToString())
    Write-Error "Grasshopper host gate failed. Logs: $runRoot`n$($_.Exception.Message)"
    exit 1
}

#requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $SourceCandidateRoot,

    [Parameter(Mandatory = $true)]
    [string] $UserGuidePdf,

    [Parameter(Mandatory = $true)]
    [string] $Food4RhinoPdf,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$expectedVersion = '0.1.0'
$packageSpecSchema = 'dragons-grasshopper.package-spec.v3'
$packageIndexSchema = 'dragons-grasshopper.package-index.v3'
$installerManifestSchema = 'dragons-grasshopper.windows-installer.v1'
$assetManifestSchema = 'dragons-grasshopper.github-release-assets.v1'
$fixedZipTimestamp = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)

function Get-NormalizedFullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    try {
        return [System.IO.Path]::GetFullPath($Path).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    }
    catch {
        throw "Path is invalid: '$Path'. $($_.Exception.Message)"
    }
}

function Test-PathAtOrBelow {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Candidate
    )

    $rootFull = Get-NormalizedFullPath -Path $Root
    $candidateFull = Get-NormalizedFullPath -Path $Candidate
    return $candidateFull.Equals(
        $rootFull,
        [System.StringComparison]::OrdinalIgnoreCase) -or
        $candidateFull.StartsWith(
            $rootFull + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-RepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Repository,

        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Label,

        [switch] $RequireLeaf,
        [switch] $RequireDirectory,
        [switch] $AllowMissing
    )

    $repositoryFull = Get-NormalizedFullPath -Path $Repository
    $candidateFull = Get-NormalizedFullPath -Path $Path
    if (-not (Test-PathAtOrBelow -Root $repositoryFull -Candidate $candidateFull)) {
        throw "$Label is outside the repository: '$candidateFull'."
    }

    $exists = Test-Path -LiteralPath $candidateFull
    if (-not $exists -and -not $AllowMissing) {
        throw "$Label is missing: '$candidateFull'."
    }
    if ($exists -and $RequireLeaf -and
        -not (Test-Path -LiteralPath $candidateFull -PathType Leaf)) {
        throw "$Label must be a file: '$candidateFull'."
    }
    if ($exists -and $RequireDirectory -and
        -not (Test-Path -LiteralPath $candidateFull -PathType Container)) {
        throw "$Label must be a directory: '$candidateFull'."
    }

    $current = $candidateFull
    while ($true) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "$Label traverses a reparse point: '$current'."
            }
        }
        if ($current.Equals(
                $repositoryFull,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or
            $parent.Equals($current, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "$Label could not be traced back to repository root '$repositoryFull'."
        }
        $current = $parent.TrimEnd('\', '/')
    }

    return $candidateFull
}

function Get-RelativeUnixPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $rootFull = Get-NormalizedFullPath -Path $Root
    $pathFull = Get-NormalizedFullPath -Path $Path
    if ($pathFull.Equals($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "A file path cannot equal its relative-path root: '$pathFull'."
    }
    if (-not $pathFull.StartsWith(
            $rootFull + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$pathFull' is outside '$rootFull'."
    }
    return $pathFull.Substring($rootFull.Length + 1).Replace('\', '/')
}

function Assert-SafeRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or
        $Path.Contains('\') -or
        $Path.StartsWith('/') -or
        $Path -match '^[A-Za-z]:' -or
        [System.IO.Path]::IsPathRooted($Path)) {
        throw "$Label is not a canonical relative Unix path: '$Path'."
    }
    $segments = @($Path.Split('/'))
    if ($segments.Count -eq 0 -or @($segments | Where-Object {
            [string]::IsNullOrWhiteSpace($_) -or $_ -eq '.' -or $_ -eq '..'
        }).Count -ne 0) {
        throw "$Label contains an unsafe path segment: '$Path'."
    }
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString(
            $sha256.ComputeHash($stream)).Replace('-', '').ToLowerInvariant())
    }
    finally {
        $sha256.Dispose()
        $stream.Dispose()
    }
}

function Get-StreamSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Stream] $Stream
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString(
            $sha256.ComputeHash($Stream)).Replace('-', '').ToLowerInvariant())
    }
    finally {
        $sha256.Dispose()
    }
}

function Write-Utf8Text {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Text
    )

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $parent -Force
    }
    [System.IO.File]::WriteAllText($Path, $Text, $script:utf8WithoutBom)
}

function ConvertTo-DeterministicJson {
    param(
        [Parameter(Mandatory = $true)]
        [object] $InputObject,

        [int] $Depth = 12
    )

    $text = $InputObject | ConvertTo-Json -Depth $Depth
    return (($text -replace "`r`n", "`n") -replace "`r", "`n") + "`n"
}

function Read-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
    try {
        $text = $strictUtf8.GetString([System.IO.File]::ReadAllBytes($Path))
        return $text | ConvertFrom-Json
    }
    catch {
        throw "$Label is not valid UTF-8 JSON: '$Path'. $($_.Exception.Message)"
    }
}

function Assert-ExactProperties {
    param(
        [Parameter(Mandatory = $true)]
        [object] $InputObject,

        [Parameter(Mandatory = $true)]
        [string[]] $Names,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    $actual = @($InputObject.PSObject.Properties | ForEach-Object { [string] $_.Name })
    if ($actual.Count -ne $Names.Count) {
        throw "$Label has $($actual.Count) properties; expected exactly $($Names.Count)."
    }
    for ($index = 0; $index -lt $Names.Count; $index += 1) {
        if ($actual[$index] -cne $Names[$index]) {
            throw "$Label property $index is '$($actual[$index])'; expected '$($Names[$index])'."
        }
    }
}

function Get-OrdinallySortedStrings {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $Values
    )

    [string[]] $copy = @($Values)
    [System.Array]::Sort($copy, [System.StringComparer]::Ordinal)
    return ,$copy
}

function Get-SafeFilesRecursive {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root
    )

    $files = New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    $pending.Push((Get-NormalizedFullPath -Path $Root))
    while ($pending.Count -ne 0) {
        $directoryPath = $pending.Pop()
        $directory = Get-Item -LiteralPath $directoryPath -Force
        if (($directory.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Candidate tree contains a reparse-point directory: '$directoryPath'."
        }
        foreach ($item in @(Get-ChildItem -LiteralPath $directoryPath -Force)) {
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Candidate tree contains a reparse point: '$($item.FullName)'."
            }
            if ($item.PSIsContainer) {
                $pending.Push($item.FullName)
            }
            else {
                $files.Add([System.IO.FileInfo] $item)
            }
        }
    }
    return @($files.ToArray())
}

function Assert-Pdf {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedName,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if ([System.IO.Path]::GetFileName($Path) -cne $ExpectedName) {
        throw "$Label filename must be '$ExpectedName'; found '$([System.IO.Path]::GetFileName($Path))'."
    }
    $item = Get-Item -LiteralPath $Path
    if ($item.Length -lt 10kb) {
        throw "$Label is unexpectedly small ($($item.Length) bytes): '$Path'."
    }
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $signature = New-Object byte[] 5
        if ($stream.Read($signature, 0, $signature.Length) -ne $signature.Length -or
            [System.Text.Encoding]::ASCII.GetString($signature) -cne '%PDF-') {
            throw "$Label does not have a PDF signature: '$Path'."
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Copy-FileExactly {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Source,

        [Parameter(Mandatory = $true)]
        [string] $Destination,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    $parent = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $parent -Force
    }
    [System.IO.File]::Copy($Source, $Destination, $false)
    $sourceItem = Get-Item -LiteralPath $Source
    $destinationItem = Get-Item -LiteralPath $Destination
    if ($sourceItem.Length -ne $destinationItem.Length -or
        (Get-Sha256 -Path $Source) -cne (Get-Sha256 -Path $Destination)) {
        throw "$Label changed while it was copied."
    }
}

function Write-Checksums {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string[]] $RelativePaths,

        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    $sorted = Get-OrdinallySortedStrings -Values $RelativePaths
    $seen = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $lines = New-Object 'System.Collections.Generic.List[string]'
    foreach ($relativePath in $sorted) {
        Assert-SafeRelativePath -Path $relativePath -Label 'Checksum path'
        if (-not $seen.Add($relativePath)) {
            throw "Checksum path is duplicate or case-ambiguous: '$relativePath'."
        }
        $path = Join-Path $Root $relativePath.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Checksum input is missing: '$path'."
        }
        $lines.Add((Get-Sha256 -Path $path) + '  ' + $relativePath)
    }
    Write-Utf8Text -Path $Destination -Text (($lines.ToArray() -join "`n") + "`n")
}

function New-DeterministicZip {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourceRoot,

        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $files = @(Get-SafeFilesRecursive -Root $SourceRoot)
    $relativePaths = @($files | ForEach-Object {
        Get-RelativeUnixPath -Root $SourceRoot -Path $_.FullName
    })
    $relativePaths = Get-OrdinallySortedStrings -Values $relativePaths

    $fileByRelativePath = @{}
    foreach ($file in $files) {
        $relativePath = Get-RelativeUnixPath -Root $SourceRoot -Path $file.FullName
        if ($fileByRelativePath.ContainsKey($relativePath)) {
            throw "ZIP source contains a duplicate relative path: '$relativePath'."
        }
        $fileByRelativePath.Add($relativePath, $file.FullName)
    }

    $destinationParent = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $destinationParent -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $destinationParent -Force
    }
    if (Test-Path -LiteralPath $Destination) {
        throw "Refusing to overwrite ZIP staging output: '$Destination'."
    }

    $stream = [System.IO.File]::Open(
        $Destination,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
    $archive = $null
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $stream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false)
        foreach ($relativePath in $relativePaths) {
            Assert-SafeRelativePath -Path $relativePath -Label 'ZIP entry path'
            $entry = $archive.CreateEntry(
                $relativePath,
                [System.IO.Compression.CompressionLevel]::NoCompression)
            $entry.LastWriteTime = $script:fixedZipTimestamp
            $input = [System.IO.File]::OpenRead([string] $fileByRelativePath[$relativePath])
            $output = $entry.Open()
            try {
                $input.CopyTo($output)
            }
            finally {
                $output.Dispose()
                $input.Dispose()
            }
        }
    }
    finally {
        if ($null -ne $archive) {
            $archive.Dispose()
        }
        $stream.Dispose()
    }
    return ,$relativePaths
}

function Get-ZipEntryBytes {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchiveEntry] $Entry
    )

    $stream = $Entry.Open()
    $memory = New-Object System.IO.MemoryStream
    try {
        $stream.CopyTo($memory)
        return [byte[]] $memory.ToArray()
    }
    finally {
        $memory.Dispose()
        $stream.Dispose()
    }
}

function Assert-InstallerManifestObject {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Manifest,

        [Parameter(Mandatory = $true)]
        [object[]] $ExpectedPackages
    )

    Assert-ExactProperties `
        -InputObject $Manifest `
        -Names @('schema', 'version', 'products') `
        -Label 'Installer manifest'
    if ([string] $Manifest.schema -cne $script:installerManifestSchema -or
        [string] $Manifest.version -cne $script:expectedVersion) {
        throw 'Installer manifest schema or version is invalid.'
    }
    $products = @($Manifest.products)
    if ($products.Count -ne 2) {
        throw 'Installer manifest must contain exactly two products.'
    }
    $expectedProductIds = @('invisible-dragon', 'simple-dragon')
    $expectedDisplayNames = @('InvisibleDragon', 'SimpleDragon')
    for ($productIndex = 0; $productIndex -lt 2; $productIndex += 1) {
        $product = $products[$productIndex]
        Assert-ExactProperties `
            -InputObject $product `
            -Names @('id', 'displayName', 'packages') `
            -Label "Installer manifest product $productIndex"
        if ([string] $product.id -cne $expectedProductIds[$productIndex] -or
            [string] $product.displayName -cne $expectedDisplayNames[$productIndex]) {
            throw "Installer manifest product $productIndex has an unexpected identity."
        }
        $packageRows = @($product.packages)
        if ($packageRows.Count -ne 2) {
            throw "Installer manifest product '$($product.id)' must contain two target packages."
        }
        for ($targetIndex = 0; $targetIndex -lt 2; $targetIndex += 1) {
            $row = $packageRows[$targetIndex]
            Assert-ExactProperties `
                -InputObject $row `
                -Names @('target', 'path', 'bytes', 'sha256') `
                -Label "Installer manifest package $($product.id)/$targetIndex"
            $expected = @($ExpectedPackages | Where-Object {
                [string] $_.productId -ceq [string] $product.id -and
                [string] $_.target -ceq [string] $row.target
            })
            if ($expected.Count -ne 1 -or
                [string] $row.target -cne @('rhino7', 'rhino8')[$targetIndex] -or
                [string] $row.path -cne [string] $expected[0].bundlePath -or
                [int64] $row.bytes -ne [int64] $expected[0].bytes -or
                [string] $row.sha256 -cne [string] $expected[0].sha256) {
                throw "Installer manifest package '$($product.id)' target $targetIndex is invalid."
            }
        }
    }
}

function Test-ExactOrdinalStringArrays {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Left,

        [Parameter(Mandatory = $true)]
        [string[]] $Right
    )

    if ($Left.Count -ne $Right.Count) {
        return $false
    }
    for ($index = 0; $index -lt $Left.Count; $index += 1) {
        if ($Left[$index] -cne $Right[$index]) {
            return $false
        }
    }
    return $true
}

function Verify-InstallerZip {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ZipPath,

        [Parameter(Mandatory = $true)]
        [string] $BundleRoot,

        [Parameter(Mandatory = $true)]
        [string[]] $ExpectedPaths,

        [Parameter(Mandatory = $true)]
        [object[]] $ExpectedPackages
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stream = [System.IO.File]::Open(
        $ZipPath,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    $archive = $null
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $stream,
            [System.IO.Compression.ZipArchiveMode]::Read,
            $false)
        $entries = @($archive.Entries)
        $exactNames = [System.Collections.Generic.HashSet[string]]::new(
            [System.StringComparer]::Ordinal)
        $foldedNames = [System.Collections.Generic.HashSet[string]]::new(
            [System.StringComparer]::OrdinalIgnoreCase)
        $entryNames = New-Object 'System.Collections.Generic.List[string]'
        foreach ($entry in $entries) {
            $entryName = [string] $entry.FullName
            Assert-SafeRelativePath -Path $entryName -Label 'ZIP entry path'
            if ([string]::IsNullOrEmpty($entry.Name)) {
                throw "Installer ZIP contains a directory entry: '$entryName'."
            }
            if (-not $exactNames.Add($entryName) -or -not $foldedNames.Add($entryName)) {
                throw "Installer ZIP contains a duplicate or case-ambiguous entry: '$entryName'."
            }
            # ZIP stores a DOS wall-clock timestamp without an offset. Reading
            # it reconstructs the local offset, so compare the stored calendar
            # fields rather than DateTimeOffset instants.
            if ($entry.LastWriteTime.Year -ne $script:fixedZipTimestamp.Year -or
                $entry.LastWriteTime.Month -ne $script:fixedZipTimestamp.Month -or
                $entry.LastWriteTime.Day -ne $script:fixedZipTimestamp.Day -or
                $entry.LastWriteTime.Hour -ne $script:fixedZipTimestamp.Hour -or
                $entry.LastWriteTime.Minute -ne $script:fixedZipTimestamp.Minute -or
                $entry.LastWriteTime.Second -ne $script:fixedZipTimestamp.Second) {
                throw "Installer ZIP entry has a non-deterministic timestamp: '$entryName'."
            }
            # The writer explicitly uses CompressionLevel.NoCompression.
            # .NET Framework may encode that choice as uncompressed DEFLATE
            # blocks, whose framing makes CompressedLength differ from Length;
            # byte equality is therefore not a portable verification signal.
            $sourcePath = Join-Path $BundleRoot $entryName.Replace('/', '\')
            if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
                throw "Installer ZIP contains an entry absent from its staged bundle: '$entryName'."
            }
            $entryStream = $entry.Open()
            try {
                $entryHash = Get-StreamSha256 -Stream $entryStream
            }
            finally {
                $entryStream.Dispose()
            }
            if ($entry.Length -ne (Get-Item -LiteralPath $sourcePath).Length -or
                $entryHash -cne (Get-Sha256 -Path $sourcePath)) {
                throw "Installer ZIP entry differs from its staged source: '$entryName'."
            }
            $entryNames.Add($entryName)
        }

        $actualPaths = Get-OrdinallySortedStrings -Values $entryNames.ToArray()
        $expectedSorted = Get-OrdinallySortedStrings -Values $ExpectedPaths
        if (-not (Test-ExactOrdinalStringArrays -Left $expectedSorted -Right $actualPaths)) {
            throw 'Installer ZIP inventory differs from the exact staged inventory.'
        }

        $manifestEntry = $archive.GetEntry('release-manifest.json')
        if ($null -eq $manifestEntry) {
            throw 'Installer ZIP has no release-manifest.json entry.'
        }
        $manifestBytes = Get-ZipEntryBytes -Entry $manifestEntry
        $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
        try {
            $manifest = $strictUtf8.GetString($manifestBytes) | ConvertFrom-Json
        }
        catch {
            throw "Embedded installer manifest is not valid UTF-8 JSON: $($_.Exception.Message)"
        }
        Assert-InstallerManifestObject `
            -Manifest $manifest `
            -ExpectedPackages $ExpectedPackages

        $checksumEntry = $archive.GetEntry('checksums.sha256')
        if ($null -eq $checksumEntry) {
            throw 'Installer ZIP has no checksums.sha256 entry.'
        }
        $embeddedChecksumBytes = Get-ZipEntryBytes -Entry $checksumEntry
        $stagedChecksumBytes = [System.IO.File]::ReadAllBytes(
            (Join-Path $BundleRoot 'checksums.sha256'))
        if ($embeddedChecksumBytes.Length -ne $stagedChecksumBytes.Length) {
            throw 'Embedded installer checksum inventory length changed.'
        }
        for ($index = 0; $index -lt $embeddedChecksumBytes.Length; $index += 1) {
            if ($embeddedChecksumBytes[$index] -ne $stagedChecksumBytes[$index]) {
                throw 'Embedded installer checksum inventory bytes changed.'
            }
        }
    }
    finally {
        if ($null -ne $archive) {
            $archive.Dispose()
        }
        $stream.Dispose()
    }
}

function Remove-GeneratedTree {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Repository,

        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedParent,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedPrefix
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }
    $full = Assert-RepositoryPath `
        -Repository $Repository `
        -Path $Path `
        -Label 'Generated cleanup path' `
        -RequireDirectory
    $parent = Get-NormalizedFullPath -Path (Split-Path -Parent $full)
    $expectedParentFull = Get-NormalizedFullPath -Path $ExpectedParent
    $name = [System.IO.Path]::GetFileName($full)
    if (-not $parent.Equals(
            $expectedParentFull,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $name.StartsWith($ExpectedPrefix, [System.StringComparison]::Ordinal)) {
        throw "Refusing to remove unexpected generated directory '$full'."
    }
    Remove-Item -LiteralPath $full -Recurse -Force
}

$RepositoryRoot = Get-NormalizedFullPath -Path $RepositoryRoot
if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
    throw "Repository root is missing: '$RepositoryRoot'."
}
$RepositoryRoot = Assert-RepositoryPath `
    -Repository $RepositoryRoot `
    -Path $RepositoryRoot `
    -Label 'Repository root' `
    -RequireDirectory
if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'packaging\package-spec.json') -PathType Leaf)) {
    throw "Repository root does not contain packaging/package-spec.json: '$RepositoryRoot'."
}

$SourceCandidateRoot = Assert-RepositoryPath `
    -Repository $RepositoryRoot `
    -Path $SourceCandidateRoot `
    -Label 'Source candidate root' `
    -RequireDirectory
$UserGuidePdf = Assert-RepositoryPath `
    -Repository $RepositoryRoot `
    -Path $UserGuidePdf `
    -Label 'User-guide PDF' `
    -RequireLeaf
$Food4RhinoPdf = Assert-RepositoryPath `
    -Repository $RepositoryRoot `
    -Path $Food4RhinoPdf `
    -Label 'Food4Rhino metadata PDF' `
    -RequireLeaf
$OutputRoot = Assert-RepositoryPath `
    -Repository $RepositoryRoot `
    -Path $OutputRoot `
    -Label 'Release asset output root' `
    -AllowMissing
if ($OutputRoot.Equals($RepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    (Test-PathAtOrBelow -Root $OutputRoot -Candidate $SourceCandidateRoot) -or
    (Test-PathAtOrBelow -Root $OutputRoot -Candidate $UserGuidePdf) -or
    (Test-PathAtOrBelow -Root $OutputRoot -Candidate $Food4RhinoPdf)) {
    throw "Release asset output root overlaps a source or the repository root: '$OutputRoot'."
}

$outputRelative = Get-RelativeUnixPath -Root $RepositoryRoot -Path $OutputRoot
$outputTopLevel = $outputRelative.Split('/')[0]
if ($outputTopLevel -cnotin @('artifacts', 'temp')) {
    throw "Release asset output root must be below repository artifacts or temp: '$OutputRoot'."
}

$packageSpecPath = Assert-RepositoryPath `
    -Repository $RepositoryRoot `
    -Path (Join-Path $RepositoryRoot 'packaging\package-spec.json') `
    -Label 'Package specification' `
    -RequireLeaf
$installerTemplatePath = Assert-RepositoryPath `
    -Repository $RepositoryRoot `
    -Path (Join-Path $RepositoryRoot 'packaging\release\Install-Dragons.cmd') `
    -Label 'Standalone installer template' `
    -RequireLeaf
$licensePath = Assert-RepositoryPath `
    -Repository $RepositoryRoot `
    -Path (Join-Path $RepositoryRoot 'LICENSE') `
    -Label 'Repository license' `
    -RequireLeaf
$noticePath = Assert-RepositoryPath `
    -Repository $RepositoryRoot `
    -Path (Join-Path $RepositoryRoot 'NOTICE.md') `
    -Label 'Repository notice' `
    -RequireLeaf

$directIndex = Join-Path $SourceCandidateRoot 'package-index.json'
$nestedIndex = Join-Path $SourceCandidateRoot 'packages\package-index.json'
$indexCandidates = @()
foreach ($candidateIndex in @($directIndex, $nestedIndex)) {
    if (Test-Path -LiteralPath $candidateIndex -PathType Leaf) {
        $indexCandidates += $candidateIndex
    }
}
if ($indexCandidates.Count -ne 1) {
    throw "Source candidate root must expose exactly one package-index.json, directly or beneath packages: '$SourceCandidateRoot'."
}
$packageIndexPath = Assert-RepositoryPath `
    -Repository $RepositoryRoot `
    -Path $indexCandidates[0] `
    -Label 'Package index' `
    -RequireLeaf
$packagesRoot = Split-Path -Parent $packageIndexPath

$spec = Read-JsonFile -Path $packageSpecPath -Label 'Package specification'
$index = Read-JsonFile -Path $packageIndexPath -Label 'Package index'
if ([string] $spec.schema -cne $packageSpecSchema -or
    [string] $spec.version -cne $expectedVersion) {
    throw "Package specification must use schema '$packageSpecSchema' and exact version '$expectedVersion'."
}
if ([string] $index.schema -cne $packageIndexSchema -or
    [string] $index.version -cne $expectedVersion) {
    throw "Package index must use schema '$packageIndexSchema' and exact version '$expectedVersion'."
}

$specTargets = @($spec.targets)
$expectedTargets = @('rhino7', 'rhino8')
if ($specTargets.Count -ne 2) {
    throw 'Package specification must contain exactly two Rhino targets.'
}
for ($targetIndex = 0; $targetIndex -lt 2; $targetIndex += 1) {
    if ([string] $specTargets[$targetIndex].id -cne $expectedTargets[$targetIndex]) {
        throw "Package specification target $targetIndex must be '$($expectedTargets[$targetIndex])'."
    }
}

$expectedProducts = @(
    [pscustomobject] [ordered] @{
        id = 'invisible-dragon'
        displayName = 'InvisibleDragon'
    },
    [pscustomobject] [ordered] @{
        id = 'simple-dragon'
        displayName = 'SimpleDragon'
    }
)
$specProducts = @($spec.products)
$indexProducts = @($index.products)
if ($specProducts.Count -ne 2 -or $indexProducts.Count -ne 2) {
    throw 'Package specification and package index must each contain exactly two products.'
}

$packageInputs = New-Object 'System.Collections.Generic.List[object]'
for ($productIndex = 0; $productIndex -lt 2; $productIndex += 1) {
    $expectedProduct = $expectedProducts[$productIndex]
    $specProduct = $specProducts[$productIndex]
    $indexProduct = $indexProducts[$productIndex]
    if ([string] $specProduct.id -cne [string] $expectedProduct.id -or
        [string] $specProduct.display_name -cne [string] $expectedProduct.displayName -or
        [string] $indexProduct.id -cne [string] $expectedProduct.id -or
        [string] $indexProduct.name -cne [string] $expectedProduct.displayName -or
        [string] $indexProduct.version -cne $expectedVersion) {
        throw "Product $productIndex does not match the exact release identity contract."
    }

    $yakRows = @($indexProduct.yak)
    if ($yakRows.Count -ne 2) {
        throw "Package index product '$($expectedProduct.id)' must contain exactly two Yak targets."
    }
    for ($targetIndex = 0; $targetIndex -lt 2; $targetIndex += 1) {
        $target = $expectedTargets[$targetIndex]
        $row = $yakRows[$targetIndex]
        $hostToken = if ($target -eq 'rhino7') { 'rh7' } else { 'rh8' }
        $expectedArtifact = '{0}/yak/{0}-{1}-{2}-win.yak' -f `
            $expectedProduct.id, $expectedVersion, $hostToken
        if ([string] $row.target -cne $target -or
            [string] $row.artifact -cne $expectedArtifact) {
            throw "Package index Yak row '$($expectedProduct.id)'/$target has a non-canonical target or artifact path."
        }
        Assert-SafeRelativePath -Path ([string] $row.artifact) -Label 'Indexed Yak artifact'
        $expectedHash = [string] $row.sha256
        if ($expectedHash -cnotmatch '^[0-9a-f]{64}$') {
            throw "Package index Yak SHA-256 is invalid for '$($expectedProduct.id)'/$target."
        }
        $sourcePath = Assert-RepositoryPath `
            -Repository $RepositoryRoot `
            -Path (Join-Path $packagesRoot ([string] $row.artifact).Replace('/', '\')) `
            -Label "Indexed Yak artifact $($expectedProduct.id)/$target" `
            -RequireLeaf
        $actualHash = Get-Sha256 -Path $sourcePath
        if ($actualHash -cne $expectedHash) {
            throw "Indexed Yak SHA-256 mismatch for '$($expectedProduct.id)'/$target."
        }
        $bundlePath = 'packages/{0}/{1}' -f $target, [System.IO.Path]::GetFileName($sourcePath)
        Assert-SafeRelativePath -Path $bundlePath -Label 'Installer bundle Yak path'
        $packageInputs.Add([pscustomobject] [ordered] @{
            productId = [string] $expectedProduct.id
            displayName = [string] $expectedProduct.displayName
            target = $target
            sourceArtifact = [string] $row.artifact
            sourcePath = $sourcePath
            bundlePath = $bundlePath
            bytes = [int64] (Get-Item -LiteralPath $sourcePath).Length
            sha256 = $actualHash
        })
    }
}

$candidateYakFiles = @(Get-SafeFilesRecursive -Root $packagesRoot | Where-Object {
    $_.Extension.Equals('.yak', [System.StringComparison]::OrdinalIgnoreCase)
})
$expectedYakPaths = Get-OrdinallySortedStrings -Values @($packageInputs | ForEach-Object {
    Get-NormalizedFullPath -Path ([string] $_.sourcePath)
})
$actualYakPaths = Get-OrdinallySortedStrings -Values @($candidateYakFiles | ForEach-Object {
    Get-NormalizedFullPath -Path $_.FullName
})
if (-not (Test-ExactOrdinalStringArrays -Left $expectedYakPaths -Right $actualYakPaths)) {
    throw 'Source candidate tree does not contain exactly the four indexed Yak files.'
}

$userGuideName = "Dragons-Grasshopper-User-Guide-$expectedVersion.pdf"
$food4RhinoName = "Dragons-Grasshopper-Food4Rhino-Metadata-$expectedVersion.pdf"
$installerZipName = "Dragons-Grasshopper-$expectedVersion-Windows-Installer.zip"
Assert-Pdf -Path $UserGuidePdf -ExpectedName $userGuideName -Label 'User-guide PDF'
Assert-Pdf -Path $Food4RhinoPdf -ExpectedName $food4RhinoName -Label 'Food4Rhino metadata PDF'

$outputParent = Split-Path -Parent $OutputRoot
$outputLeaf = [System.IO.Path]::GetFileName($OutputRoot)
if ([string]::IsNullOrWhiteSpace($outputLeaf)) {
    throw "Release asset output root must have a leaf directory name: '$OutputRoot'."
}
if (-not (Test-Path -LiteralPath $outputParent -PathType Container)) {
    $null = New-Item -ItemType Directory -Path $outputParent -Force
}
$outputParent = Assert-RepositoryPath `
    -Repository $RepositoryRoot `
    -Path $outputParent `
    -Label 'Release asset output parent' `
    -RequireDirectory
$stagingPrefix = '.' + $outputLeaf + '.staging-'
$backupPrefix = '.' + $outputLeaf + '.backup-'
$stagingRoot = Join-Path $outputParent ($stagingPrefix + [Guid]::NewGuid().ToString('N'))
$backupRoot = Join-Path $outputParent ($backupPrefix + [Guid]::NewGuid().ToString('N'))
$publicRoot = Join-Path $stagingRoot 'github-assets'
$bundleRoot = Join-Path $stagingRoot '.installer-bundle'
$internalManifestPath = Join-Path $stagingRoot 'release-assets-manifest.json'
$published = $false
$existingMoved = $false

try {
    $null = New-Item -ItemType Directory -Path $publicRoot -Force
    $null = New-Item -ItemType Directory -Path $bundleRoot -Force

    Copy-FileExactly `
        -Source $installerTemplatePath `
        -Destination (Join-Path $bundleRoot 'Install-Dragons.cmd') `
        -Label 'Standalone installer template'
    Copy-FileExactly `
        -Source $licensePath `
        -Destination (Join-Path $bundleRoot 'LICENSE.txt') `
        -Label 'Repository license'
    Copy-FileExactly `
        -Source $noticePath `
        -Destination (Join-Path $bundleRoot 'NOTICE.md') `
        -Label 'Repository notice'

    foreach ($packageInput in $packageInputs) {
        Copy-FileExactly `
            -Source ([string] $packageInput.sourcePath) `
            -Destination (Join-Path $bundleRoot ([string] $packageInput.bundlePath).Replace('/', '\')) `
            -Label "Yak package $($packageInput.productId)/$($packageInput.target)"
    }

    $manifestProducts = @()
    foreach ($expectedProduct in $expectedProducts) {
        $manifestPackages = @($packageInputs | Where-Object {
            [string] $_.productId -ceq [string] $expectedProduct.id
        } | ForEach-Object {
            [pscustomobject] [ordered] @{
                target = [string] $_.target
                path = [string] $_.bundlePath
                bytes = [int64] $_.bytes
                sha256 = [string] $_.sha256
            }
        })
        $manifestProducts += [pscustomobject] [ordered] @{
            id = [string] $expectedProduct.id
            displayName = [string] $expectedProduct.displayName
            packages = $manifestPackages
        }
    }
    $installerManifest = [pscustomobject] [ordered] @{
        schema = $installerManifestSchema
        version = $expectedVersion
        products = $manifestProducts
    }
    Assert-InstallerManifestObject `
        -Manifest $installerManifest `
        -ExpectedPackages $packageInputs.ToArray()
    Write-Utf8Text `
        -Path (Join-Path $bundleRoot 'release-manifest.json') `
        -Text (ConvertTo-DeterministicJson -InputObject $installerManifest -Depth 8)

    $readme = @"
Dragons Grasshopper $expectedVersion for Windows

This archive contains the verified InvisibleDragon and SimpleDragon packages
for Grasshopper on Rhino 7 and Rhino 8.

Installation

1. Extract this entire ZIP to a normal local directory.
2. Close every running Rhino process.
3. Run Install-Dragons.cmd from the extracted directory.

The installer finds the four Yak files only through release-manifest.json and
paths relative to its own directory. It verifies their SHA-256 values before
removing or installing either Dragon package. Rhino itself is not installed.

Requirements: Windows x64 and an installed Rhino 7 or Rhino 8 with Grasshopper.
See LICENSE.txt and NOTICE.md before redistribution.
"@
    Write-Utf8Text `
        -Path (Join-Path $bundleRoot 'README.txt') `
        -Text (($readme.TrimEnd() -replace "`r`n", "`n") + "`n")

    $bundlePathsWithoutChecksums = @(
        Get-SafeFilesRecursive -Root $bundleRoot | ForEach-Object {
            Get-RelativeUnixPath -Root $bundleRoot -Path $_.FullName
        }
    )
    Write-Checksums `
        -Root $bundleRoot `
        -RelativePaths $bundlePathsWithoutChecksums `
        -Destination (Join-Path $bundleRoot 'checksums.sha256')

    $expectedBundlePaths = @(
        Get-SafeFilesRecursive -Root $bundleRoot | ForEach-Object {
            Get-RelativeUnixPath -Root $bundleRoot -Path $_.FullName
        }
    )
    $expectedBundlePaths = Get-OrdinallySortedStrings -Values $expectedBundlePaths
    $canonicalExpectedBundlePaths = Get-OrdinallySortedStrings -Values @(
        'Install-Dragons.cmd',
        'LICENSE.txt',
        'NOTICE.md',
        'README.txt',
        'checksums.sha256',
        'release-manifest.json',
        'packages/rhino7/invisible-dragon-0.1.0-rh7-win.yak',
        'packages/rhino7/simple-dragon-0.1.0-rh7-win.yak',
        'packages/rhino8/invisible-dragon-0.1.0-rh8-win.yak',
        'packages/rhino8/simple-dragon-0.1.0-rh8-win.yak'
    )
    if (-not (Test-ExactOrdinalStringArrays `
            -Left $canonicalExpectedBundlePaths `
            -Right $expectedBundlePaths)) {
        throw 'Installer bundle staging inventory is not the exact ten-file contract.'
    }

    $installerZipPath = Join-Path $publicRoot $installerZipName
    $writtenZipPaths = New-DeterministicZip `
        -SourceRoot $bundleRoot `
        -Destination $installerZipPath
    if (-not (Test-ExactOrdinalStringArrays `
            -Left $expectedBundlePaths `
            -Right $writtenZipPaths)) {
        throw 'Deterministic ZIP writer did not consume the exact bundle inventory.'
    }
    Verify-InstallerZip `
        -ZipPath $installerZipPath `
        -BundleRoot $bundleRoot `
        -ExpectedPaths $expectedBundlePaths `
        -ExpectedPackages $packageInputs.ToArray()

    $publicUserGuidePath = Join-Path $publicRoot $userGuideName
    $publicFood4RhinoPath = Join-Path $publicRoot $food4RhinoName
    Copy-FileExactly `
        -Source $UserGuidePdf `
        -Destination $publicUserGuidePath `
        -Label 'User-guide PDF'
    Copy-FileExactly `
        -Source $Food4RhinoPdf `
        -Destination $publicFood4RhinoPath `
        -Label 'Food4Rhino metadata PDF'

    Write-Checksums `
        -Root $publicRoot `
        -RelativePaths @($installerZipName, $userGuideName, $food4RhinoName) `
        -Destination (Join-Path $publicRoot 'SHA256SUMS.txt')

    $publicFiles = @(Get-SafeFilesRecursive -Root $publicRoot)
    $publicNames = Get-OrdinallySortedStrings -Values @($publicFiles | ForEach-Object { $_.Name })
    $expectedPublicNames = Get-OrdinallySortedStrings -Values @(
        $installerZipName,
        $userGuideName,
        $food4RhinoName,
        'SHA256SUMS.txt'
    )
    if (-not (Test-ExactOrdinalStringArrays -Left $expectedPublicNames -Right $publicNames) -or
        $publicFiles.Count -ne 4) {
        throw 'Public github-assets directory must contain exactly the four declared assets.'
    }

    $assetRoles = @{
        $installerZipName = 'windows-installer'
        $userGuideName = 'user-guide'
        $food4RhinoName = 'food4rhino-metadata'
        'SHA256SUMS.txt' = 'checksums'
    }
    $publicAssetRows = @($expectedPublicNames | ForEach-Object {
        $path = Join-Path $publicRoot $_
        [pscustomobject] [ordered] @{
            role = [string] $assetRoles[$_]
            fileName = $_
            path = 'github-assets/' + $_
            bytes = [int64] (Get-Item -LiteralPath $path).Length
            sha256 = Get-Sha256 -Path $path
        }
    })
    $sourceRows = @(
        [pscustomobject] [ordered] @{
            role = 'package-spec'
            path = Get-RelativeUnixPath -Root $RepositoryRoot -Path $packageSpecPath
            bytes = [int64] (Get-Item -LiteralPath $packageSpecPath).Length
            sha256 = Get-Sha256 -Path $packageSpecPath
        },
        [pscustomobject] [ordered] @{
            role = 'package-index'
            path = Get-RelativeUnixPath -Root $RepositoryRoot -Path $packageIndexPath
            bytes = [int64] (Get-Item -LiteralPath $packageIndexPath).Length
            sha256 = Get-Sha256 -Path $packageIndexPath
        },
        [pscustomobject] [ordered] @{
            role = 'installer-template'
            path = Get-RelativeUnixPath -Root $RepositoryRoot -Path $installerTemplatePath
            bytes = [int64] (Get-Item -LiteralPath $installerTemplatePath).Length
            sha256 = Get-Sha256 -Path $installerTemplatePath
        },
        [pscustomobject] [ordered] @{
            role = 'license'
            path = Get-RelativeUnixPath -Root $RepositoryRoot -Path $licensePath
            bytes = [int64] (Get-Item -LiteralPath $licensePath).Length
            sha256 = Get-Sha256 -Path $licensePath
        },
        [pscustomobject] [ordered] @{
            role = 'notice'
            path = Get-RelativeUnixPath -Root $RepositoryRoot -Path $noticePath
            bytes = [int64] (Get-Item -LiteralPath $noticePath).Length
            sha256 = Get-Sha256 -Path $noticePath
        },
        [pscustomobject] [ordered] @{
            role = 'user-guide-source'
            path = Get-RelativeUnixPath -Root $RepositoryRoot -Path $UserGuidePdf
            bytes = [int64] (Get-Item -LiteralPath $UserGuidePdf).Length
            sha256 = Get-Sha256 -Path $UserGuidePdf
        },
        [pscustomobject] [ordered] @{
            role = 'food4rhino-metadata-source'
            path = Get-RelativeUnixPath -Root $RepositoryRoot -Path $Food4RhinoPdf
            bytes = [int64] (Get-Item -LiteralPath $Food4RhinoPdf).Length
            sha256 = Get-Sha256 -Path $Food4RhinoPdf
        }
    )
    $internalManifest = [pscustomobject] [ordered] @{
        schema = $assetManifestSchema
        version = $expectedVersion
        publicDirectory = 'github-assets'
        installerManifestSchema = $installerManifestSchema
        installerInventory = $expectedBundlePaths
        packageInputs = @($packageInputs | ForEach-Object {
            [pscustomobject] [ordered] @{
                product = [string] $_.productId
                target = [string] $_.target
                sourceArtifact = [string] $_.sourceArtifact
                bundlePath = [string] $_.bundlePath
                bytes = [int64] $_.bytes
                sha256 = [string] $_.sha256
            }
        })
        sources = $sourceRows
        assets = $publicAssetRows
    }
    Write-Utf8Text `
        -Path $internalManifestPath `
        -Text (ConvertTo-DeterministicJson -InputObject $internalManifest -Depth 12)

    Remove-GeneratedTree `
        -Repository $RepositoryRoot `
        -Path $bundleRoot `
        -ExpectedParent $stagingRoot `
        -ExpectedPrefix '.installer-bundle'
    $stagingItems = @(Get-ChildItem -LiteralPath $stagingRoot -Force)
    if ($stagingItems.Count -ne 2 -or
        -not (Test-Path -LiteralPath $publicRoot -PathType Container) -or
        -not (Test-Path -LiteralPath $internalManifestPath -PathType Leaf)) {
        throw 'Release asset output staging must contain only github-assets and its internal manifest.'
    }

    if (Test-Path -LiteralPath $OutputRoot) {
        $null = Assert-RepositoryPath `
            -Repository $RepositoryRoot `
            -Path $OutputRoot `
            -Label 'Existing release asset output root' `
            -RequireDirectory
        Move-Item -LiteralPath $OutputRoot -Destination $backupRoot
        $existingMoved = $true
    }
    try {
        Move-Item -LiteralPath $stagingRoot -Destination $OutputRoot
        $published = $true
    }
    catch {
        if ($existingMoved -and
            -not (Test-Path -LiteralPath $OutputRoot) -and
            (Test-Path -LiteralPath $backupRoot -PathType Container)) {
            Move-Item -LiteralPath $backupRoot -Destination $OutputRoot
            $existingMoved = $false
        }
        throw
    }

    if ($existingMoved) {
        Remove-GeneratedTree `
            -Repository $RepositoryRoot `
            -Path $backupRoot `
            -ExpectedParent $outputParent `
            -ExpectedPrefix $backupPrefix
        $existingMoved = $false
    }
}
finally {
    if (-not $published -and (Test-Path -LiteralPath $stagingRoot -PathType Container)) {
        Remove-GeneratedTree `
            -Repository $RepositoryRoot `
            -Path $stagingRoot `
            -ExpectedParent $outputParent `
            -ExpectedPrefix $stagingPrefix
    }
    if ($existingMoved -and
        (Test-Path -LiteralPath $backupRoot -PathType Container) -and
        -not (Test-Path -LiteralPath $OutputRoot)) {
        Move-Item -LiteralPath $backupRoot -Destination $OutputRoot
    }
}

$publishedPublicRoot = Join-Path $OutputRoot 'github-assets'
$publishedManifest = Join-Path $OutputRoot 'release-assets-manifest.json'
Write-Host "GitHub release assets: $publishedPublicRoot"
Write-Host "Internal release asset manifest: $publishedManifest"
Write-Host "Version: $expectedVersion"
Write-Host 'Public asset count: 4'

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sizes = @(24, 48, 64, 128, 256)
$products = @(
    [pscustomobject]@{
        Slug = 'invisible-dragon'
        Source = Join-Path $repositoryRoot 'assets\icons\source\invisible-dragon.png'
        PackageDirectory = Join-Path $repositoryRoot 'packaging\invisible-dragon'
    },
    [pscustomobject]@{
        Slug = 'simple-dragon'
        Source = Join-Path $repositoryRoot 'assets\icons\source\simple-dragon.png'
        PackageDirectory = Join-Path $repositoryRoot 'packaging\simple-dragon'
    }
)

function Write-ScaledPng {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Image] $SourceImage,

        [Parameter(Mandatory = $true)]
        [ValidateRange(1, 4096)]
        [int] $Size,

        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppPArgb)

    try {
        $bitmap.SetResolution(96.0, 96.0)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

            $destinationRectangle = [System.Drawing.Rectangle]::new(0, 0, $Size, $Size)
            $sourceRectangle = [System.Drawing.Rectangle]::new(
                0,
                0,
                $SourceImage.Width,
                $SourceImage.Height)
            $attributes = [System.Drawing.Imaging.ImageAttributes]::new()

            try {
                $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
                $graphics.DrawImage(
                    $SourceImage,
                    $destinationRectangle,
                    $sourceRectangle.X,
                    $sourceRectangle.Y,
                    $sourceRectangle.Width,
                    $sourceRectangle.Height,
                    [System.Drawing.GraphicsUnit]::Pixel,
                    $attributes)
            }
            finally {
                $attributes.Dispose()
            }
        }
        finally {
            $graphics.Dispose()
        }

        $destinationDirectory = Split-Path -Parent $Destination
        [System.IO.Directory]::CreateDirectory($destinationDirectory) | Out-Null
        $bitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

foreach ($product in $products) {
    if (-not (Test-Path -LiteralPath $product.Source -PathType Leaf)) {
        throw "Icon source does not exist: $($product.Source)"
    }

    $sourceImage = [System.Drawing.Image]::FromFile($product.Source)

    try {
        if ($sourceImage.Width -ne $sourceImage.Height) {
            throw "Icon source must be square: $($product.Source)"
        }

        $generatedDirectory = Join-Path $repositoryRoot "assets\icons\generated\$($product.Slug)"

        foreach ($size in $sizes) {
            $destination = Join-Path $generatedDirectory "$($product.Slug)-$size.png"
            Write-ScaledPng -SourceImage $sourceImage -Size $size -Destination $destination
            Write-Host "Generated $destination"
        }
    }
    finally {
        $sourceImage.Dispose()
    }

    [System.IO.Directory]::CreateDirectory($product.PackageDirectory) | Out-Null
    $packageIcon = Join-Path $product.PackageDirectory 'icon.png'
    $generatedPackageIcon = Join-Path $generatedDirectory "$($product.Slug)-256.png"
    Copy-Item -LiteralPath $generatedPackageIcon -Destination $packageIcon -Force
    Write-Host "Updated $packageIcon"
}

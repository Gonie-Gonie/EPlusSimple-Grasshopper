[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sizes = @(24, 48, 64, 128, 256)

function New-ComponentIcon {
    param(
        [Parameter(Mandatory = $true)] [string] $Name,
        [Parameter(Mandatory = $true)] [ValidateRange(0, 15)] [int] $Tile,
        [Parameter(Mandatory = $true)] [string] $Overlay
    )

    [pscustomobject]@{ Name = $Name; Tile = $Tile; Overlay = $Overlay }
}

$products = @(
    [pscustomobject]@{
        Slug = 'invisible-dragon'
        Source = Join-Path $repositoryRoot 'assets\icons\source\invisible-dragon.png'
        Atlas = Join-Path $repositoryRoot 'assets\icons\illustrated\invisible-dragon-functional-atlas.png'
        PackageDirectory = Join-Path $repositoryRoot 'packaging\invisible-dragon'
        BadgeColor = [System.Drawing.Color]::FromArgb(235, 46, 35, 94)
        Components = @(
            (New-ComponentIcon 'InvisibleDragonVersionComponent' 0 'none'),
            (New-ComponentIcon 'OpaqueMaterialComponent' 1 'none'),
            (New-ComponentIcon 'LayeredConstructionComponent' 2 'none'),
            (New-ComponentIcon 'NoMassConstructionComponent' 2 'membrane'),
            (New-ComponentIcon 'ConstantProfileComponent' 3 'none'),
            (New-ComponentIcon 'SurfaceFromPolylineComponent' 4 'polyline'),
            (New-ComponentIcon 'ZoneComponent' 6 'none'),
            (New-ComponentIcon 'EnergyModelComponent' 7 'assemble'),
            (New-ComponentIcon 'CompileIdfComponent' 8 'compile'),
            (New-ComponentIcon 'ValidateIdfComponent' 9 'none'),
            (New-ComponentIcon 'PrepareEnergyPlusRuntimeComponent' 10 'none'),
            (New-ComponentIcon 'RunEnergyPlusComponent' 11 'none'),
            (New-ComponentIcon 'ReadEnergyPlusResultsComponent' 8 'read'),
            (New-ComponentIcon 'EnergyPlusResultSummaryComponent' 15 'none'),
            (New-ComponentIcon 'HeatPumpComponent' 12 'heat-pump'),
            (New-ComponentIcon 'GeothermalHeatPumpComponent' 12 'ground'),
            (New-ComponentIcon 'CoolingTowerComponent' 14 'cooling-tower'),
            (New-ComponentIcon 'ChillerComponent' 14 'snowflake'),
            (New-ComponentIcon 'AbsorptionChillerComponent' 14 'absorption'),
            (New-ComponentIcon 'BoilerComponent' 13 'none'),
            (New-ComponentIcon 'DistrictHeatingComponent' 13 'network'),
            (New-ComponentIcon 'PackagedAirConditionerComponent' 12 'packaged'),
            (New-ComponentIcon 'AirHandlingUnitComponent' 12 'ahu'),
            (New-ComponentIcon 'FanCoilUnitComponent' 12 'coil'),
            (New-ComponentIcon 'RadiatorComponent' 13 'radiator'),
            (New-ComponentIcon 'ElectricRadiatorComponent' 13 'electric-radiator'),
            (New-ComponentIcon 'RadiantFloorComponent' 13 'radiant-floor'),
            (New-ComponentIcon 'ElectricRadiantFloorComponent' 13 'electric-floor'),
            (New-ComponentIcon 'EnergyRecoveryVentilatorComponent' 12 'erv'),
            (New-ComponentIcon 'PhotovoltaicPanelComponent' 7 'photovoltaic'),
            (New-ComponentIcon 'SupplyGroupAssignmentComponent' 12 'assign'))
    },
    [pscustomobject]@{
        Slug = 'simple-dragon'
        Source = Join-Path $repositoryRoot 'assets\icons\source\simple-dragon.png'
        Atlas = Join-Path $repositoryRoot 'assets\icons\illustrated\simple-dragon-functional-atlas.png'
        PackageDirectory = Join-Path $repositoryRoot 'packaging\simple-dragon'
        BadgeColor = [System.Drawing.Color]::FromArgb(235, 8, 79, 75)
        Components = @(
            (New-ComponentIcon 'SimpleDragonVersionComponent' 0 'none'),
            (New-ComponentIcon 'SimpleDragonMaterialComponent' 1 'none'),
            (New-ComponentIcon 'SimpleDragonSurfaceConstructionComponent' 2 'none'),
            (New-ComponentIcon 'SimpleDragonFenestrationConstructionComponent' 3 'none'),
            (New-ComponentIcon 'LookupUsageProfileComponent' 4 'none'),
            (New-ComponentIcon 'ExtractSimpleDragonZonesComponent' 6 'extract'),
            (New-ComponentIcon 'SimpleDragonHeatPumpComponent' 7 'heat-pump'),
            (New-ComponentIcon 'SimpleDragonGeothermalHeatPumpComponent' 7 'ground'),
            (New-ComponentIcon 'SimpleDragonChillerComponent' 7 'snowflake'),
            (New-ComponentIcon 'SimpleDragonAbsorptionChillerComponent' 7 'absorption'),
            (New-ComponentIcon 'SimpleDragonBoilerComponent' 7 'flame'),
            (New-ComponentIcon 'SimpleDragonDistrictHeatingComponent' 7 'network'),
            (New-ComponentIcon 'SimpleDragonPackagedAirConditionerComponent' 7 'packaged'),
            (New-ComponentIcon 'SimpleDragonAirHandlingUnitComponent' 7 'ahu'),
            (New-ComponentIcon 'SimpleDragonFanCoilUnitComponent' 7 'coil'),
            (New-ComponentIcon 'SimpleDragonRadiatorComponent' 7 'radiator'),
            (New-ComponentIcon 'SimpleDragonElectricRadiatorComponent' 7 'electric-radiator'),
            (New-ComponentIcon 'SimpleDragonRadiantFloorComponent' 7 'radiant-floor'),
            (New-ComponentIcon 'SimpleDragonElectricRadiantFloorComponent' 7 'electric-floor'),
            (New-ComponentIcon 'SimpleDragonEnergyRecoveryVentilatorComponent' 7 'erv'),
            (New-ComponentIcon 'SimpleDragonPhotovoltaicPanelComponent' 7 'photovoltaic'),
            (New-ComponentIcon 'AssignSimpleDragonVentilationSystemsComponent' 7 'assign-air'),
            (New-ComponentIcon 'AssignSimpleDragonSupplySystemsComponent' 7 'assign-supply'),
            (New-ComponentIcon 'AssembleGreenRetrofitModelComponent' 8 'assemble'),
            (New-ComponentIcon 'ReadGreenRetrofitModelComponent' 8 'read'),
            (New-ComponentIcon 'WriteGreenRetrofitModelComponent' 8 'write'),
            (New-ComponentIcon 'ConvertGreenRetrofitModelComponent' 9 'convert'),
            (New-ComponentIcon 'BuildGreenRetrofitResultComponent' 10 'build'),
            (New-ComponentIcon 'ReadGreenRetrofitResultComponent' 10 'read'),
            (New-ComponentIcon 'WriteGreenRetrofitResultComponent' 10 'write'),
            (New-ComponentIcon 'GreenRetrofitResultSummaryComponent' 11 'none'),
            (New-ComponentIcon 'GreenRetrofitDataTreeComponent' 12 'none'),
            (New-ComponentIcon 'GreenRetrofitMonthlyLinePlotComponent' 13 'none'),
            (New-ComponentIcon 'GreenRetrofitMonthlyBarPlotComponent' 14 'none'),
            (New-ComponentIcon 'ExportGreenRetrofitCsvComponent' 15 'none'),
            (New-ComponentIcon 'RunSimpleDragonBatchComponent' 8 'batch'))
    })

function New-TransparentBitmap {
    param(
        [Parameter(Mandatory = $true)] [ValidateRange(1, 4096)] [int] $Width,
        [Parameter(Mandatory = $true)] [ValidateRange(1, 4096)] [int] $Height
    )

    $bitmap = [System.Drawing.Bitmap]::new(
        $Width,
        $Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppPArgb)
    $bitmap.SetResolution(96.0, 96.0)
    return $bitmap
}

function Set-HighQualityDrawing {
    param([Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics)

    $Graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $Graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $Graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $Graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
}

function Draw-ImageRegion {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics,
        [Parameter(Mandatory = $true)] [System.Drawing.Image] $SourceImage,
        [Parameter(Mandatory = $true)] [System.Drawing.Rectangle] $DestinationRectangle,
        [Parameter(Mandatory = $true)] [System.Drawing.Rectangle] $SourceRectangle
    )

    $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
    try {
        $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
        $Graphics.DrawImage(
            $SourceImage,
            $DestinationRectangle,
            $SourceRectangle.X,
            $SourceRectangle.Y,
            $SourceRectangle.Width,
            $SourceRectangle.Height,
            [System.Drawing.GraphicsUnit]::Pixel,
            $attributes)
    }
    finally {
        $attributes.Dispose()
    }
}

function Write-ScaledPng {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Image] $SourceImage,
        [Parameter(Mandatory = $true)] [ValidateRange(1, 4096)] [int] $Size,
        [Parameter(Mandatory = $true)] [string] $Destination
    )

    $bitmap = New-TransparentBitmap $Size $Size
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            Set-HighQualityDrawing $graphics
            Draw-ImageRegion $graphics $SourceImage `
                ([System.Drawing.Rectangle]::new(0, 0, $Size, $Size)) `
                ([System.Drawing.Rectangle]::new(0, 0, $SourceImage.Width, $SourceImage.Height))
        }
        finally {
            $graphics.Dispose()
        }

        [System.IO.Directory]::CreateDirectory((Split-Path -Parent $Destination)) | Out-Null
        $bitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function Get-AtlasCell {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Bitmap] $Atlas,
        [Parameter(Mandatory = $true)] [ValidateRange(0, 15)] [int] $Tile
    )

    $row = [int][Math]::Floor($Tile / 4.0)
    $column = $Tile % 4
    $left = [int][Math]::Floor($column * $Atlas.Width / 4.0)
    $top = [int][Math]::Floor($row * $Atlas.Height / 4.0)
    $right = [int][Math]::Floor(($column + 1) * $Atlas.Width / 4.0)
    $bottom = [int][Math]::Floor(($row + 1) * $Atlas.Height / 4.0)
    return [System.Drawing.Rectangle]::FromLTRB($left + 2, $top + 2, $right - 2, $bottom - 2)
}

function Get-OpaqueBounds {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Bitmap] $Atlas,
        [Parameter(Mandatory = $true)] [System.Drawing.Rectangle] $Cell
    )

    $left = $Cell.Right
    $top = $Cell.Bottom
    $right = $Cell.Left - 1
    $bottom = $Cell.Top - 1
    for ($y = $Cell.Top; $y -lt $Cell.Bottom; $y++) {
        for ($x = $Cell.Left; $x -lt $Cell.Right; $x++) {
            if ($Atlas.GetPixel($x, $y).A -gt 48) {
                if ($x -lt $left) { $left = $x }
                if ($x -gt $right) { $right = $x }
                if ($y -lt $top) { $top = $y }
                if ($y -gt $bottom) { $bottom = $y }
            }
        }
    }

    if ($right -lt $left -or $bottom -lt $top) {
        throw "Atlas cell has no visible pixels: $Cell"
    }

    return [System.Drawing.Rectangle]::FromLTRB($left, $top, $right + 1, $bottom + 1)
}

function New-WhitePen {
    param([float] $Width = 5.0)

    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, $Width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function Draw-Arrow {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics,
        [Parameter(Mandatory = $true)] [System.Drawing.Pen] $Pen,
        [float] $X1, [float] $Y1, [float] $X2, [float] $Y2
    )

    $Graphics.DrawLine($Pen, $X1, $Y1, $X2, $Y2)
    $angle = [Math]::Atan2($Y2 - $Y1, $X2 - $X1)
    $wing = 6.5
    foreach ($delta in @(2.55, -2.55)) {
        $wingX = $X2 + [Math]::Cos($angle + $delta) * $wing
        $wingY = $Y2 + [Math]::Sin($angle + $delta) * $wing
        $Graphics.DrawLine($Pen, $X2, $Y2, [float]$wingX, [float]$wingY)
    }
}

function Draw-Snowflake {
    param([System.Drawing.Graphics] $Graphics, [System.Drawing.Pen] $Pen)

    $Graphics.DrawLine($Pen, 61, 71, 81, 71)
    $Graphics.DrawLine($Pen, 66, 62, 76, 80)
    $Graphics.DrawLine($Pen, 76, 62, 66, 80)
}

function Draw-Lightning {
    param([System.Drawing.Graphics] $Graphics, [System.Drawing.Pen] $Pen)

    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(73, 59),
        [System.Drawing.PointF]::new(65, 71),
        [System.Drawing.PointF]::new(72, 71),
        [System.Drawing.PointF]::new(67, 83),
        [System.Drawing.PointF]::new(80, 67),
        [System.Drawing.PointF]::new(73, 67))
    $Graphics.DrawLines($Pen, $points)
}

function Draw-Overlay {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics,
        [Parameter(Mandatory = $true)] [string] $Kind,
        [Parameter(Mandatory = $true)] [System.Drawing.Color] $BadgeColor
    )

    if ($Kind -eq 'none') { return }

    $badgeBrush = [System.Drawing.SolidBrush]::new($BadgeColor)
    $borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(235, 9, 18, 29), 3.0)
    $whitePen = New-WhitePen
    $thinPen = New-WhitePen 3.2
    $accentPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 255, 180, 45), 4.2)
    $accentPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $accentPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    try {
        $Graphics.FillEllipse($badgeBrush, 53, 53, 36, 36)
        $Graphics.DrawEllipse($borderPen, 53, 53, 36, 36)

        switch ($Kind) {
            'membrane' {
                foreach ($x in @(59, 67, 75)) { $Graphics.DrawLine($whitePen, $x, 72, $x + 4, 72) }
            }
            'polyline' {
                $Graphics.DrawLines($thinPen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(59, 80), [System.Drawing.PointF]::new(64, 63),
                    [System.Drawing.PointF]::new(73, 76), [System.Drawing.PointF]::new(82, 60)))
                foreach ($point in @(@(59,80), @(64,63), @(73,76), @(82,60))) {
                    $Graphics.FillEllipse([System.Drawing.Brushes]::White, $point[0] - 2, $point[1] - 2, 5, 5)
                }
            }
            { $_ -in @('assemble', 'build') } {
                $Graphics.DrawLine($whitePen, 61, 71, 81, 71)
                $Graphics.DrawLine($whitePen, 71, 61, 71, 81)
                if ($Kind -eq 'assemble') { $Graphics.DrawEllipse($thinPen, 58, 58, 26, 26) }
            }
            'compile' {
                $Graphics.DrawLines($whitePen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(67, 60), [System.Drawing.PointF]::new(59, 71),
                    [System.Drawing.PointF]::new(67, 82)))
                $Graphics.DrawLines($whitePen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(75, 60), [System.Drawing.PointF]::new(83, 71),
                    [System.Drawing.PointF]::new(75, 82)))
            }
            'read' { Draw-Arrow $Graphics $whitePen 81 60 61 81 }
            'write' { Draw-Arrow $Graphics $whitePen 61 81 81 60 }
            'extract' {
                $Graphics.DrawRectangle($thinPen, 59, 62, 15, 17)
                Draw-Arrow $Graphics $whitePen 70 72 83 60
            }
            'convert' {
                Draw-Arrow $Graphics $thinPen 59 66 81 66
                Draw-Arrow $Graphics $thinPen 82 76 60 76
            }
            'batch' {
                foreach ($offset in @(0, 5, 10)) { $Graphics.DrawLine($thinPen, 59, 61 + $offset, 72, 61 + $offset) }
                $Graphics.FillPolygon([System.Drawing.Brushes]::White, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(72, 72), [System.Drawing.PointF]::new(72, 84),
                    [System.Drawing.PointF]::new(84, 78)))
            }
            'heat-pump' {
                Draw-Arrow $Graphics $thinPen 61 67 79 63
                Draw-Arrow $Graphics $thinPen 81 75 63 79
            }
            'ground' {
                foreach ($y in @(65, 72, 79)) { $Graphics.DrawLine($thinPen, 58, $y, 84, $y) }
                Draw-Arrow $Graphics $thinPen 71 59 71 82
            }
            'cooling-tower' {
                $Graphics.DrawLines($whitePen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(64, 59), [System.Drawing.PointF]::new(60, 82),
                    [System.Drawing.PointF]::new(82, 82), [System.Drawing.PointF]::new(78, 59)))
                $Graphics.FillEllipse([System.Drawing.Brushes]::DeepSkyBlue, 66, 64, 5, 7)
                $Graphics.FillEllipse([System.Drawing.Brushes]::DeepSkyBlue, 73, 70, 5, 7)
            }
            'snowflake' { Draw-Snowflake $Graphics $whitePen }
            'absorption' {
                Draw-Snowflake $Graphics $thinPen
                $Graphics.DrawEllipse($accentPen, 73, 73, 8, 10)
            }
            'flame' {
                $Graphics.DrawLines($accentPen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(71, 59), [System.Drawing.PointF]::new(63, 73),
                    [System.Drawing.PointF]::new(70, 83), [System.Drawing.PointF]::new(80, 73),
                    [System.Drawing.PointF]::new(75, 65), [System.Drawing.PointF]::new(71, 75)))
            }
            { $_ -in @('network', 'assign', 'assign-air', 'assign-supply') } {
                $networkPen = if ($Kind -eq 'assign-air') {
                    [System.Drawing.Pen]::new([System.Drawing.Color]::DeepSkyBlue, 4.0)
                } elseif ($Kind -eq 'assign-supply') {
                    [System.Drawing.Pen]::new([System.Drawing.Color]::Orange, 4.0)
                } else { New-WhitePen 4.0 }
                try {
                    $Graphics.DrawLine($networkPen, 59, 71, 70, 71)
                    $Graphics.DrawLine($networkPen, 70, 71, 82, 61)
                    $Graphics.DrawLine($networkPen, 70, 71, 82, 81)
                    foreach ($point in @(@(59,71), @(82,61), @(82,81))) {
                        $Graphics.FillEllipse([System.Drawing.Brushes]::White, $point[0] - 2, $point[1] - 2, 5, 5)
                    }
                }
                finally { $networkPen.Dispose() }
            }
            'packaged' {
                $Graphics.DrawRectangle($whitePen, 59, 59, 24, 24)
                $Graphics.DrawLine($thinPen, 64, 71, 78, 71)
                $Graphics.DrawLine($thinPen, 71, 64, 71, 78)
            }
            'ahu' {
                $Graphics.DrawRectangle($whitePen, 58, 63, 19, 16)
                Draw-Arrow $Graphics $thinPen 72 71 84 71
            }
            'coil' {
                $Graphics.DrawArc($whitePen, 57, 61, 14, 20, -90, 180)
                $Graphics.DrawArc($whitePen, 68, 61, 14, 20, 90, 180)
            }
            { $_ -in @('radiator', 'electric-radiator') } {
                foreach ($x in @(60, 66, 72, 78)) { $Graphics.DrawLine($thinPen, $x, 61, $x, 81) }
                if ($Kind -eq 'electric-radiator') { Draw-Lightning $Graphics $accentPen }
            }
            { $_ -in @('radiant-floor', 'electric-floor') } {
                foreach ($y in @(73, 79, 84)) { $Graphics.DrawLine($thinPen, 58, $y, 84, $y) }
                if ($Kind -eq 'electric-floor') {
                    Draw-Lightning $Graphics $accentPen
                } else {
                    Draw-Arrow $Graphics $thinPen 64 72 64 60
                    Draw-Arrow $Graphics $thinPen 77 72 77 60
                }
            }
            'erv' {
                Draw-Arrow $Graphics $thinPen 59 63 82 78
                Draw-Arrow $Graphics $thinPen 82 63 59 78
            }
            'photovoltaic' {
                $Graphics.DrawRectangle($thinPen, 58, 66, 22, 16)
                $Graphics.DrawLine($thinPen, 65, 66, 65, 82)
                $Graphics.DrawLine($thinPen, 72, 66, 72, 82)
                $Graphics.DrawLine($thinPen, 58, 74, 80, 74)
                $Graphics.DrawEllipse($accentPen, 76, 57, 7, 7)
            }
            default { throw "Unknown icon overlay '$Kind'." }
        }
    }
    finally {
        $accentPen.Dispose()
        $thinPen.Dispose()
        $whitePen.Dispose()
        $borderPen.Dispose()
        $badgeBrush.Dispose()
    }
}

function Write-ComponentPng {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Bitmap] $Atlas,
        [Parameter(Mandatory = $true)] [ValidateRange(0, 15)] [int] $Tile,
        [Parameter(Mandatory = $true)] [string] $Overlay,
        [Parameter(Mandatory = $true)] [System.Drawing.Color] $BadgeColor,
        [Parameter(Mandatory = $true)] [string] $Destination
    )

    $source = Get-OpaqueBounds $Atlas (Get-AtlasCell $Atlas $Tile)
    $maximum = 76.0
    $scale = [Math]::Min($maximum / $source.Width, $maximum / $source.Height)
    $width = [Math]::Max(1, [int][Math]::Round($source.Width * $scale))
    $height = [Math]::Max(1, [int][Math]::Round($source.Height * $scale))
    $destinationRectangle = [System.Drawing.Rectangle]::new(
        [int][Math]::Floor((96 - $width) / 2.0),
        [int][Math]::Floor((96 - $height) / 2.0),
        $width,
        $height)

    $working = New-TransparentBitmap 96 96
    $result = New-TransparentBitmap 24 24
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($working)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            Set-HighQualityDrawing $graphics
            Draw-ImageRegion $graphics $Atlas $destinationRectangle $source
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
            Draw-Overlay $graphics $Overlay $BadgeColor
        }
        finally { $graphics.Dispose() }

        $graphics = [System.Drawing.Graphics]::FromImage($result)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            Set-HighQualityDrawing $graphics
            Draw-ImageRegion $graphics $working `
                ([System.Drawing.Rectangle]::new(0, 0, 24, 24)) `
                ([System.Drawing.Rectangle]::new(0, 0, 96, 96))
        }
        finally { $graphics.Dispose() }

        for ($pixel = 0; $pixel -lt 24; $pixel++) {
            foreach ($edge in @(0, 1, 22, 23)) {
                $result.SetPixel($edge, $pixel, [System.Drawing.Color]::Transparent)
                $result.SetPixel($pixel, $edge, [System.Drawing.Color]::Transparent)
            }
        }

        [System.IO.Directory]::CreateDirectory((Split-Path -Parent $Destination)) | Out-Null
        $result.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $result.Dispose()
        $working.Dispose()
    }
}

function Write-ContactSheet {
    param(
        [Parameter(Mandatory = $true)] [string] $ProductSlug,
        [Parameter(Mandatory = $true)] [object[]] $Components,
        [Parameter(Mandatory = $true)] [string] $ComponentDirectory,
        [Parameter(Mandatory = $true)] [string] $Destination
    )

    $columns = 4
    $cellWidth = 360
    $cellHeight = 78
    $headerHeight = 48
    $rows = [int][Math]::Ceiling($Components.Count / [double]$columns)
    $sheet = [System.Drawing.Bitmap]::new($columns * $cellWidth, $headerHeight + $rows * $cellHeight)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($sheet)
        try {
            $graphics.Clear([System.Drawing.Color]::FromArgb(255, 245, 247, 250))
            Set-HighQualityDrawing $graphics
            $headingFont = [System.Drawing.Font]::new('Segoe UI', 16, [System.Drawing.FontStyle]::Bold)
            $labelFont = [System.Drawing.Font]::new('Segoe UI', 8, [System.Drawing.FontStyle]::Regular)
            $headingBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 20, 31, 48))
            $labelBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 26, 38, 57))
            $gridPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 218, 225, 234), 1)
            try {
                $graphics.DrawString("$ProductSlug component icons", $headingFont, $headingBrush, 12, 10)
                for ($index = 0; $index -lt $Components.Count; $index++) {
                    $component = $Components[$index]
                    $column = $index % $columns
                    $row = [int][Math]::Floor($index / [double]$columns)
                    $x = $column * $cellWidth
                    $y = $headerHeight + $row * $cellHeight
                    $graphics.DrawRectangle($gridPen, $x, $y, $cellWidth, $cellHeight)
                    $icon = [System.Drawing.Image]::FromFile((Join-Path $ComponentDirectory "$($component.Name).png"))
                    try {
                        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                        $graphics.DrawImage($icon, $x + 12, $y + 12, 48, 48)
                        Set-HighQualityDrawing $graphics
                    }
                    finally { $icon.Dispose() }

                    $graphics.DrawString(
                        ($component.Name -replace 'Component$', ''),
                        $labelFont,
                        $labelBrush,
                        $x + 70,
                        $y + 22)
                }
            }
            finally {
                $gridPen.Dispose()
                $labelBrush.Dispose()
                $headingBrush.Dispose()
                $labelFont.Dispose()
                $headingFont.Dispose()
            }
        }
        finally { $graphics.Dispose() }

        $sheet.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $sheet.Dispose() }
}

function Assert-ComponentIcons {
    param(
        [Parameter(Mandatory = $true)] [object[]] $Components,
        [Parameter(Mandatory = $true)] [string] $ComponentDirectory
    )

    $hashes = @{}
    foreach ($component in $Components) {
        $path = Join-Path $ComponentDirectory "$($component.Name).png"
        $bitmap = [System.Drawing.Bitmap]::new($path)
        try {
            if ($bitmap.Width -ne 24 -or $bitmap.Height -ne 24) {
                throw "Component icon must be exactly 24x24: $path"
            }

            for ($pixel = 0; $pixel -lt 24; $pixel++) {
                foreach ($edge in @(0, 1, 22, 23)) {
                    if ($bitmap.GetPixel($edge, $pixel).A -ne 0 -or $bitmap.GetPixel($pixel, $edge).A -ne 0) {
                        throw "Component icon must preserve a two-pixel transparent border: $path"
                    }
                }
            }
        }
        finally { $bitmap.Dispose() }

        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($hashes.ContainsKey($hash)) {
            throw "Component icons must be visually distinct: '$($hashes[$hash])' and '$($component.Name)'."
        }
        $hashes[$hash] = $component.Name
    }
}

foreach ($product in $products) {
    foreach ($requiredFile in @($product.Source, $product.Atlas)) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Icon source does not exist: $requiredFile"
        }
    }

    $duplicateNames = @($product.Components | Group-Object Name | Where-Object Count -ne 1)
    if ($duplicateNames.Count -ne 0) {
        throw "Duplicate component icon names for $($product.Slug): $($duplicateNames.Name -join ', ')"
    }

    $generatedDirectory = Join-Path $repositoryRoot "assets\icons\generated\$($product.Slug)"
    $sourceImage = [System.Drawing.Image]::FromFile($product.Source)
    try {
        if ($sourceImage.Width -ne $sourceImage.Height) {
            throw "Icon source must be square: $($product.Source)"
        }
        foreach ($size in $sizes) {
            $destination = Join-Path $generatedDirectory "$($product.Slug)-$size.png"
            Write-ScaledPng $sourceImage $size $destination
            Write-Host "Generated $destination"
        }
    }
    finally { $sourceImage.Dispose() }

    $atlas = [System.Drawing.Bitmap]::new($product.Atlas)
    try {
        $componentDirectory = Join-Path $generatedDirectory 'components'
        [System.IO.Directory]::CreateDirectory($componentDirectory) | Out-Null
        $expectedPaths = [System.Collections.Generic.HashSet[string]]::new(
            [System.StringComparer]::OrdinalIgnoreCase)
        foreach ($component in $product.Components) {
            $destination = Join-Path $componentDirectory "$($component.Name).png"
            [void]$expectedPaths.Add([System.IO.Path]::GetFullPath($destination))
            Write-ComponentPng $atlas $component.Tile $component.Overlay $product.BadgeColor $destination
            Write-Host "Generated $destination"
        }
        foreach ($existing in [System.IO.Directory]::GetFiles($componentDirectory, '*.png')) {
            if (-not $expectedPaths.Contains([System.IO.Path]::GetFullPath($existing))) {
                Remove-Item -LiteralPath $existing -Force
                Write-Host "Removed stale component icon $existing"
            }
        }

        Assert-ComponentIcons $product.Components $componentDirectory
        $contactSheet = Join-Path $generatedDirectory "$($product.Slug)-component-contact-sheet.png"
        Write-ContactSheet $product.Slug $product.Components $componentDirectory $contactSheet
        Write-Host "Generated $contactSheet"
    }
    finally { $atlas.Dispose() }

    [System.IO.Directory]::CreateDirectory($product.PackageDirectory) | Out-Null
    $packageIcon = Join-Path $product.PackageDirectory 'icon.png'
    Copy-Item `
        -LiteralPath (Join-Path $generatedDirectory "$($product.Slug)-256.png") `
        -Destination $packageIcon `
        -Force
    Write-Host "Updated $packageIcon"
}

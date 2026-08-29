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
        [Parameter(Mandatory = $true)] [string] $Overlay,
        [string] $Illustration = ''
    )

    [pscustomobject]@{
        Name = $Name
        Tile = $Tile
        Overlay = $Overlay
        Illustration = $Illustration
    }
}

$products = @(
    [pscustomobject]@{
        Slug = 'invisible-dragon'
        Source = Join-Path $repositoryRoot 'assets\icons\source\invisible-dragon.png'
        Atlas = Join-Path $repositoryRoot 'assets\icons\illustrated\invisible-dragon-functional-atlas.png'
        PackageDirectory = Join-Path $repositoryRoot 'packaging\invisible-dragon'
        Palette = [pscustomobject]@{
            Style = 'spectral'
            Backplate = [System.Drawing.Color]::FromArgb(224, 13, 25, 61)
            Border = [System.Drawing.Color]::FromArgb(255, 52, 224, 255)
            Primary = [System.Drawing.Color]::FromArgb(255, 47, 220, 255)
            Secondary = [System.Drawing.Color]::FromArgb(255, 139, 77, 255)
            Accent = [System.Drawing.Color]::FromArgb(255, 255, 183, 42)
            Hot = [System.Drawing.Color]::FromArgb(255, 255, 78, 45)
            Neutral = [System.Drawing.Color]::FromArgb(255, 244, 251, 255)
            Ink = [System.Drawing.Color]::FromArgb(255, 7, 15, 35)
        }
        Components = @(
            (New-ComponentIcon 'InvisibleDragonVersionComponent' 0 'none'),
            (New-ComponentIcon 'OpaqueMaterialComponent' 1 'none'),
            (New-ComponentIcon 'ConstructionLayerComponent' 2 'layer'),
            (New-ComponentIcon 'LayeredConstructionComponent' 2 'none'),
            (New-ComponentIcon 'NoMassConstructionComponent' 2 'membrane'),
            (New-ComponentIcon 'ConstantProfileComponent' 3 'none'),
            (New-ComponentIcon 'GlazingComponent' 3 'glazing'),
            (New-ComponentIcon 'WindowFromPolylineComponent' 4 'window'),
            (New-ComponentIcon 'DoorFromPolylineComponent' 4 'door'),
            (New-ComponentIcon 'SurfaceComponent' 4 'surface'),
            (New-ComponentIcon 'ZoneComponent' 6 'none'),
            (New-ComponentIcon 'EnergyModelComponent' 7 'assemble'),
            (New-ComponentIcon 'CompileInvisibleDragonComponent' 9 'build'),
            (New-ComponentIcon 'ManagedRunEnergyPlusComponent' 11 'batch'),
            (New-ComponentIcon 'ReadEnergyPlusResultsComponent' 8 'read'),
            (New-ComponentIcon 'EnergyPlusResultSummaryComponent' 15 'none'),
            (New-ComponentIcon 'HeatPumpComponent' 12 'heat-pump'),
            (New-ComponentIcon 'GeothermalHeatPumpComponent' 12 'ground'),
            (New-ComponentIcon 'CoolingTowerComponent' 14 'cooling-tower'),
            (New-ComponentIcon 'ChillerComponent' 14 'snowflake'),
            (New-ComponentIcon 'AbsorptionChillerComponent' 14 'absorption'),
            (New-ComponentIcon 'BoilerComponent' 13 'flame'),
            (New-ComponentIcon `
                'DomesticHotWaterComponent' `
                13 `
                'none' `
                (Join-Path $repositoryRoot 'assets\icons\illustrated\invisible-dragon-domestic-hot-water.png')),
            (New-ComponentIcon 'DistrictHeatingComponent' 13 'network'),
            (New-ComponentIcon 'PackagedAirConditionerComponent' 12 'packaged'),
            (New-ComponentIcon 'AirHandlingUnitComponent' 12 'ahu'),
            (New-ComponentIcon 'FanCoilUnitComponent' 12 'coil'),
            (New-ComponentIcon 'RadiatorComponent' 13 'radiator'),
            (New-ComponentIcon 'ElectricRadiatorComponent' 13 'electric-radiator'),
            (New-ComponentIcon 'RadiantFloorComponent' 13 'radiant-floor'),
            (New-ComponentIcon 'ElectricRadiantFloorComponent' 13 'electric-floor'),
            (New-ComponentIcon 'EnergyRecoveryVentilatorComponent' 12 'erv'),
            (New-ComponentIcon 'PhotovoltaicPanelComponent' 7 'photovoltaic'))
        Parameters = @(
            (New-ComponentIcon 'DragonMaterialParam' 1 'none'),
            (New-ComponentIcon 'DragonLayerParam' 2 'layer'),
            (New-ComponentIcon 'DragonConstructionParam' 2 'none'),
            (New-ComponentIcon 'DragonGlazingParam' 3 'glazing'),
            (New-ComponentIcon 'DragonScheduleParam' 3 'none'),
            (New-ComponentIcon 'DragonProfileParam' 3 'profile'),
            (New-ComponentIcon 'DragonOpeningParam' 4 'window'),
            (New-ComponentIcon 'DragonSurfaceParam' 4 'surface'),
            (New-ComponentIcon 'DragonZoneDefinitionParam' 6 'none'),
            (New-ComponentIcon 'DragonEnergyModelParam' 7 'assemble'),
            (New-ComponentIcon 'DragonSourceSystemParam' 13 'network'),
            (New-ComponentIcon 'DragonSupplySystemParam' 12 'assign'),
            (New-ComponentIcon `
                'DragonDomesticHotWaterParam' `
                13 `
                'none' `
                (Join-Path $repositoryRoot 'assets\icons\illustrated\invisible-dragon-domestic-hot-water.png')),
            (New-ComponentIcon 'DragonEnergyRecoveryVentilatorParam' 12 'erv'),
            (New-ComponentIcon 'DragonPhotovoltaicPanelParam' 7 'photovoltaic'),
            (New-ComponentIcon 'DragonIdfParam' 8 'none'),
            (New-ComponentIcon 'EnergyPlusResultParam' 15 'none'),
            (New-ComponentIcon 'PreparedWeatherFileParam' 11 'read'),
            (New-ComponentIcon 'DiagnosticParam' 9 'none'))
    },
    [pscustomobject]@{
        Slug = 'simple-dragon'
        Source = Join-Path $repositoryRoot 'assets\icons\source\simple-dragon.png'
        Atlas = Join-Path $repositoryRoot 'assets\icons\illustrated\simple-dragon-functional-atlas.png'
        PackageDirectory = Join-Path $repositoryRoot 'packaging\simple-dragon'
        Palette = [pscustomobject]@{
            Style = 'origami'
            Backplate = [System.Drawing.Color]::FromArgb(238, 246, 244, 218)
            Border = [System.Drawing.Color]::FromArgb(255, 9, 91, 78)
            Primary = [System.Drawing.Color]::FromArgb(255, 12, 157, 150)
            Secondary = [System.Drawing.Color]::FromArgb(255, 49, 170, 79)
            Accent = [System.Drawing.Color]::FromArgb(255, 255, 174, 28)
            Hot = [System.Drawing.Color]::FromArgb(255, 236, 76, 27)
            Neutral = [System.Drawing.Color]::FromArgb(255, 255, 252, 231)
            Ink = [System.Drawing.Color]::FromArgb(255, 17, 65, 62)
        }
        Components = @(
            (New-ComponentIcon 'SimpleDragonVersionComponent' 0 'none'),
            (New-ComponentIcon 'SimpleDragonMaterialComponent' 1 'none'),
            (New-ComponentIcon 'SimpleDragonSurfaceConstructionLayerComponent' 2 'layer'),
            (New-ComponentIcon 'SimpleDragonSurfaceConstructionComponent' 2 'none'),
            (New-ComponentIcon 'SimpleDragonFenestrationConstructionComponent' 3 'none'),
            (New-ComponentIcon 'LookupUsageProfileComponent' 4 'none'),
            (New-ComponentIcon 'CreateSimpleDragonOpeningComponent' 5 'polyline'),
            (New-ComponentIcon 'CreateSimpleDragonZoneComponent' 6 'membrane'),
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
            (New-ComponentIcon 'CreateSimpleDragonModelComponent' 8 'model-compose'),
            (New-ComponentIcon 'ReadGreenRetrofitModelComponent' 8 'read-model'),
            (New-ComponentIcon 'WriteGreenRetrofitModelComponent' 8 'write-model'),
            (New-ComponentIcon 'PrepareSimpleDragonSimulationComponent' 9 'compile'),
            (New-ComponentIcon 'BuildGreenRetrofitResultComponent' 10 'build'),
            (New-ComponentIcon 'ReadGreenRetrofitResultComponent' 10 'read-result'),
            (New-ComponentIcon 'WriteGreenRetrofitResultComponent' 10 'write-result'),
            (New-ComponentIcon 'GreenRetrofitResultSummaryComponent' 11 'none'),
            (New-ComponentIcon 'GreenRetrofitDataTreeComponent' 12 'none'),
            (New-ComponentIcon 'GreenRetrofitMonthlyLinePlotComponent' 13 'none'),
            (New-ComponentIcon 'GreenRetrofitMonthlyBarPlotComponent' 14 'none'),
            (New-ComponentIcon 'ExportGreenRetrofitCsvComponent' 15 'none'),
            (New-ComponentIcon 'SimpleDragonBatchCaseComponent' 9 'batch-case'),
            (New-ComponentIcon 'ManagedRunSimpleDragonBatchComponent' 9 'managed-batch'))
        Parameters = @(
            (New-ComponentIcon 'SimpleDragonMaterialParam' 1 'none'),
            (New-ComponentIcon 'SimpleDragonSurfaceConstructionLayerParam' 2 'layer'),
            (New-ComponentIcon 'SimpleDragonSurfaceConstructionParam' 2 'none'),
            (New-ComponentIcon 'SimpleDragonFenestrationConstructionParam' 3 'none'),
            (New-ComponentIcon 'SimpleDragonUsageProfileParam' 4 'none'),
            (New-ComponentIcon 'SimpleDragonSurfaceParam' 5 'none'),
            (New-ComponentIcon 'SimpleDragonZoneParam' 6 'none'),
            (New-ComponentIcon 'SimpleDragonOpeningDefinitionParam' 5 'polyline'),
            (New-ComponentIcon 'SimpleDragonZoneDefinitionParam' 6 'assemble'),
            (New-ComponentIcon 'SimpleDragonSourceSystemParam' 7 'network'),
            (New-ComponentIcon 'SimpleDragonSupplySystemParam' 7 'assign'),
            (New-ComponentIcon 'SimpleDragonZoneErvParam' 7 'erv'),
            (New-ComponentIcon 'SimpleDragonPhotovoltaicPanelParam' 7 'photovoltaic'),
            (New-ComponentIcon 'GreenRetrofitModelParam' 8 'none'),
            (New-ComponentIcon 'GreenRetrofitResultParam' 10 'none'),
            (New-ComponentIcon 'SimpleDragonBatchCaseParam' 9 'batch'))
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

function New-RoundPen {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Color] $Color,
        [float] $Width = 5.0
    )

    $pen = [System.Drawing.Pen]::new($Color, $Width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function Draw-Arrow {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics,
        [Parameter(Mandatory = $true)] [System.Drawing.Pen] $Pen,
        [float] $X1, [float] $Y1, [float] $X2, [float] $Y2,
        [float] $Wing = 8.0
    )

    $Graphics.DrawLine($Pen, $X1, $Y1, $X2, $Y2)
    $angle = [Math]::Atan2($Y2 - $Y1, $X2 - $X1)
    foreach ($delta in @(2.55, -2.55)) {
        $wingX = $X2 + [Math]::Cos($angle + $delta) * $Wing
        $wingY = $Y2 + [Math]::Sin($angle + $delta) * $Wing
        $Graphics.DrawLine($Pen, $X2, $Y2, [float]$wingX, [float]$wingY)
    }
}

function Draw-Snowflake {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics,
        [Parameter(Mandatory = $true)] [System.Drawing.Pen] $Pen,
        [float] $CenterX = 48,
        [float] $CenterY = 48,
        [float] $Radius = 27
    )

    foreach ($angle in @(0.0, 60.0, 120.0)) {
        $radians = $angle * [Math]::PI / 180.0
        $x = [Math]::Cos($radians) * $Radius
        $y = [Math]::Sin($radians) * $Radius
        $Graphics.DrawLine(
            $Pen,
            [float]($CenterX - $x),
            [float]($CenterY - $y),
            [float]($CenterX + $x),
            [float]($CenterY + $y))
    }

    $branch = $Radius * 0.27
    foreach ($angle in @(0.0, 60.0, 120.0, 180.0, 240.0, 300.0)) {
        $radians = $angle * [Math]::PI / 180.0
        $tipX = $CenterX + [Math]::Cos($radians) * $Radius
        $tipY = $CenterY + [Math]::Sin($radians) * $Radius
        foreach ($branchAngle in @(($angle + 145.0), ($angle - 145.0))) {
            $branchRadians = $branchAngle * [Math]::PI / 180.0
            $Graphics.DrawLine(
                $Pen,
                [float]$tipX,
                [float]$tipY,
                [float]($tipX + [Math]::Cos($branchRadians) * $branch),
                [float]($tipY + [Math]::Sin($branchRadians) * $branch))
        }
    }
}

function Draw-Lightning {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics,
        [Parameter(Mandatory = $true)] [System.Drawing.Pen] $Pen,
        [float] $X = 32,
        [float] $Y = 18,
        [float] $Width = 34,
        [float] $Height = 60
    )

    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($X + $Width * 0.66, $Y),
        [System.Drawing.PointF]::new($X + $Width * 0.18, $Y + $Height * 0.50),
        [System.Drawing.PointF]::new($X + $Width * 0.52, $Y + $Height * 0.50),
        [System.Drawing.PointF]::new($X + $Width * 0.28, $Y + $Height),
        [System.Drawing.PointF]::new($X + $Width * 0.84, $Y + $Height * 0.39),
        [System.Drawing.PointF]::new($X + $Width * 0.55, $Y + $Height * 0.39))
    $Graphics.DrawLines($Pen, $points)
}

function Draw-Flame {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics,
        [Parameter(Mandatory = $true)] [System.Drawing.Brush] $FillBrush,
        [Parameter(Mandatory = $true)] [System.Drawing.Brush] $InnerBrush,
        [Parameter(Mandatory = $true)] [System.Drawing.Pen] $OutlinePen,
        [float] $X,
        [float] $Y,
        [float] $Width,
        [float] $Height
    )

    $outer = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($X + $Width * 0.55, $Y),
        [System.Drawing.PointF]::new($X + $Width * 0.18, $Y + $Height * 0.49),
        [System.Drawing.PointF]::new($X + $Width * 0.26, $Y + $Height * 0.82),
        [System.Drawing.PointF]::new($X + $Width * 0.50, $Y + $Height),
        [System.Drawing.PointF]::new($X + $Width * 0.79, $Y + $Height * 0.78),
        [System.Drawing.PointF]::new($X + $Width * 0.86, $Y + $Height * 0.43),
        [System.Drawing.PointF]::new($X + $Width * 0.68, $Y + $Height * 0.20),
        [System.Drawing.PointF]::new($X + $Width * 0.60, $Y + $Height * 0.58),
        [System.Drawing.PointF]::new($X + $Width * 0.42, $Y + $Height * 0.38))
    $Graphics.FillPolygon($FillBrush, $outer)
    $Graphics.DrawPolygon($OutlinePen, $outer)
    $inner = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($X + $Width * 0.50, $Y + $Height * 0.47),
        [System.Drawing.PointF]::new($X + $Width * 0.36, $Y + $Height * 0.75),
        [System.Drawing.PointF]::new($X + $Width * 0.50, $Y + $Height * 0.90),
        [System.Drawing.PointF]::new($X + $Width * 0.66, $Y + $Height * 0.72))
    $Graphics.FillPolygon($InnerBrush, $inner)
}

function Draw-Fan {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics,
        [Parameter(Mandatory = $true)] [System.Drawing.Brush] $BladeBrush,
        [Parameter(Mandatory = $true)] [System.Drawing.Brush] $HubBrush,
        [Parameter(Mandatory = $true)] [System.Drawing.Pen] $OutlinePen,
        [float] $CenterX,
        [float] $CenterY,
        [float] $Radius
    )

    $Graphics.DrawEllipse(
        $OutlinePen,
        $CenterX - $Radius,
        $CenterY - $Radius,
        $Radius * 2,
        $Radius * 2)
    foreach ($angle in @(0.0, 120.0, 240.0)) {
        $radians = $angle * [Math]::PI / 180.0
        $next = ($angle + 46.0) * [Math]::PI / 180.0
        $blade = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(
                $CenterX + [Math]::Cos($radians) * $Radius * 0.18,
                $CenterY + [Math]::Sin($radians) * $Radius * 0.18),
            [System.Drawing.PointF]::new(
                $CenterX + [Math]::Cos($radians) * $Radius * 0.90,
                $CenterY + [Math]::Sin($radians) * $Radius * 0.90),
            [System.Drawing.PointF]::new(
                $CenterX + [Math]::Cos($next) * $Radius * 0.55,
                $CenterY + [Math]::Sin($next) * $Radius * 0.55))
        $Graphics.FillPolygon($BladeBrush, $blade)
    }
    $Graphics.FillEllipse($HubBrush, $CenterX - 5, $CenterY - 5, 10, 10)
}

function Draw-FunctionalBackplate {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics,
        [Parameter(Mandatory = $true)] [object] $Palette
    )

    $backplateBrush = [System.Drawing.SolidBrush]::new($Palette.Backplate)
    $borderPen = New-RoundPen $Palette.Border 5.5
    $secondaryPen = New-RoundPen $Palette.Secondary 3.5
    try {
        if ([string] $Palette.Style -eq 'spectral') {
            $Graphics.FillEllipse($backplateBrush, 9, 9, 78, 78)
            $Graphics.DrawArc($borderPen, 9, 9, 78, 78, 195, 245)
            $Graphics.DrawArc($secondaryPen, 13, 13, 70, 70, 15, 135)
        }
        else {
            $hexagon = [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new(48, 7),
                [System.Drawing.PointF]::new(83, 27),
                [System.Drawing.PointF]::new(83, 69),
                [System.Drawing.PointF]::new(48, 89),
                [System.Drawing.PointF]::new(13, 69),
                [System.Drawing.PointF]::new(13, 27))
            $Graphics.FillPolygon($backplateBrush, $hexagon)
            $Graphics.DrawPolygon($borderPen, $hexagon)
            $foldBrush = [System.Drawing.SolidBrush]::new($Palette.Accent)
            try {
                $Graphics.FillPolygon($foldBrush, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(13, 27),
                    [System.Drawing.PointF]::new(31, 18),
                    [System.Drawing.PointF]::new(22, 40)))
            }
            finally { $foldBrush.Dispose() }
        }
    }
    finally {
        $secondaryPen.Dispose()
        $borderPen.Dispose()
        $backplateBrush.Dispose()
    }
}

function Draw-Overlay {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics,
        [Parameter(Mandatory = $true)] [string] $Kind,
        [Parameter(Mandatory = $true)] [object] $Palette
    )

    if ($Kind -eq 'none') { return }

    Draw-FunctionalBackplate $Graphics $Palette

    $primaryBrush = [System.Drawing.SolidBrush]::new($Palette.Primary)
    $secondaryBrush = [System.Drawing.SolidBrush]::new($Palette.Secondary)
    $accentBrush = [System.Drawing.SolidBrush]::new($Palette.Accent)
    $hotBrush = [System.Drawing.SolidBrush]::new($Palette.Hot)
    $neutralBrush = [System.Drawing.SolidBrush]::new($Palette.Neutral)
    $inkBrush = [System.Drawing.SolidBrush]::new($Palette.Ink)
    $primaryPen = New-RoundPen $Palette.Primary 6.0
    $secondaryPen = New-RoundPen $Palette.Secondary 6.0
    $accentPen = New-RoundPen $Palette.Accent 6.0
    $hotPen = New-RoundPen $Palette.Hot 6.0
    $neutralPen = New-RoundPen $Palette.Neutral 5.0
    $inkPen = New-RoundPen $Palette.Ink 5.5
    $thinInkPen = New-RoundPen $Palette.Ink 3.5
    try {
        switch ($Kind) {
            'membrane' {
                $Graphics.FillRectangle($neutralBrush, 20, 24, 56, 48)
                $Graphics.DrawRectangle($inkPen, 20, 24, 56, 48)
                foreach ($y in @(34, 46, 58)) {
                    $Graphics.DrawLine($primaryPen, 25, $y, 71, $y)
                }
                $Graphics.DrawLine($accentPen, 28, 67, 68, 28)
            }
            'layer' {
                $Graphics.FillPolygon($primaryBrush, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(16, 29), [System.Drawing.PointF]::new(65, 17),
                    [System.Drawing.PointF]::new(81, 29), [System.Drawing.PointF]::new(32, 42)))
                $Graphics.FillPolygon($secondaryBrush, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(16, 45), [System.Drawing.PointF]::new(65, 33),
                    [System.Drawing.PointF]::new(81, 45), [System.Drawing.PointF]::new(32, 58)))
                $Graphics.FillPolygon($accentBrush, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(16, 61), [System.Drawing.PointF]::new(65, 49),
                    [System.Drawing.PointF]::new(81, 61), [System.Drawing.PointF]::new(32, 74)))
                foreach ($offset in @(0, 16, 32)) {
                    $Graphics.DrawPolygon($thinInkPen, [System.Drawing.PointF[]]@(
                        [System.Drawing.PointF]::new(16, 29 + $offset),
                        [System.Drawing.PointF]::new(65, 17 + $offset),
                        [System.Drawing.PointF]::new(81, 29 + $offset),
                        [System.Drawing.PointF]::new(32, 42 + $offset)))
                }
            }
            'glazing' {
                $Graphics.FillRectangle($primaryBrush, 19, 19, 58, 58)
                $Graphics.DrawRectangle($inkPen, 19, 19, 58, 58)
                $Graphics.DrawLine($neutralPen, 48, 22, 48, 74)
                $Graphics.DrawLine($neutralPen, 22, 48, 74, 48)
                $Graphics.FillEllipse($accentBrush, 65, 10, 20, 20)
                $Graphics.DrawEllipse($thinInkPen, 65, 10, 20, 20)
                $Graphics.DrawLine($accentPen, 16, 78, 78, 16)
            }
            'window' {
                $Graphics.FillRectangle($neutralBrush, 14, 16, 68, 66)
                $Graphics.DrawRectangle($inkPen, 14, 16, 68, 66)
                $Graphics.FillRectangle($primaryBrush, 25, 27, 46, 42)
                $Graphics.DrawRectangle($thinInkPen, 25, 27, 46, 42)
                $Graphics.DrawLine($neutralPen, 48, 29, 48, 67)
                $Graphics.DrawLine($neutralPen, 27, 48, 69, 48)
                $Graphics.DrawLine($accentPen, 19, 78, 77, 78)
            }
            'door' {
                $Graphics.FillRectangle($neutralBrush, 22, 14, 52, 68)
                $Graphics.DrawRectangle($inkPen, 22, 14, 52, 68)
                $Graphics.FillPolygon($primaryBrush, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(29, 22), [System.Drawing.PointF]::new(61, 30),
                    [System.Drawing.PointF]::new(61, 75), [System.Drawing.PointF]::new(29, 75)))
                $Graphics.DrawPolygon($thinInkPen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(29, 22), [System.Drawing.PointF]::new(61, 30),
                    [System.Drawing.PointF]::new(61, 75), [System.Drawing.PointF]::new(29, 75)))
                $Graphics.FillEllipse($accentBrush, 51, 49, 8, 8)
                $Graphics.DrawArc($secondaryPen, 43, 23, 38, 55, -90, 90)
            }
            'surface' {
                $face = [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(17, 69), [System.Drawing.PointF]::new(29, 20),
                    [System.Drawing.PointF]::new(79, 28), [System.Drawing.PointF]::new(68, 77))
                $Graphics.FillPolygon($secondaryBrush, $face)
                $Graphics.DrawPolygon($inkPen, $face)
                $Graphics.DrawLine($primaryPen, 24, 49, 73, 56)
                $Graphics.DrawLine($accentPen, 29, 70, 66, 27)
                foreach ($point in @(@(17,69), @(29,20), @(79,28), @(68,77))) {
                    $Graphics.FillEllipse($neutralBrush, $point[0] - 4, $point[1] - 4, 8, 8)
                }
            }
            'batch-case' {
                $Graphics.FillRectangle($neutralBrush, 18, 14, 60, 68)
                $Graphics.DrawRectangle($inkPen, 18, 14, 60, 68)
                $Graphics.FillRectangle($primaryBrush, 27, 25, 20, 17)
                $Graphics.DrawRectangle($thinInkPen, 27, 25, 20, 17)
                $Graphics.FillRectangle($secondaryBrush, 49, 33, 20, 17)
                $Graphics.DrawRectangle($thinInkPen, 49, 33, 20, 17)
                $Graphics.DrawLine($accentPen, 27, 59, 67, 59)
                $Graphics.DrawLine($accentPen, 27, 69, 56, 69)
                $Graphics.FillEllipse($hotBrush, 64, 62, 20, 20)
                $Graphics.DrawEllipse($thinInkPen, 64, 62, 20, 20)
            }
            'polyline' {
                $Graphics.DrawLines($neutralPen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(17, 72), [System.Drawing.PointF]::new(30, 24),
                    [System.Drawing.PointF]::new(51, 62), [System.Drawing.PointF]::new(78, 19)))
                foreach ($point in @(@(17,72), @(30,24), @(51,62), @(78,19))) {
                    $Graphics.FillEllipse($primaryBrush, $point[0] - 6, $point[1] - 6, 12, 12)
                    $Graphics.DrawEllipse($inkPen, $point[0] - 6, $point[1] - 6, 12, 12)
                }
            }
            'assemble' {
                foreach ($box in @(@(17,24), @(17,58), @(59,41))) {
                    $Graphics.FillRectangle($primaryBrush, $box[0], $box[1], 20, 16)
                    $Graphics.DrawRectangle($inkPen, $box[0], $box[1], 20, 16)
                }
                $Graphics.DrawLine($neutralPen, 37, 32, 58, 48)
                $Graphics.DrawLine($neutralPen, 37, 66, 58, 50)
                Draw-Arrow $Graphics $accentPen 64 49 78 49 7
            }
            'build' {
                $Graphics.FillRectangle($neutralBrush, 24, 17, 48, 62)
                $Graphics.DrawRectangle($inkPen, 24, 17, 48, 62)
                $Graphics.DrawLine($primaryPen, 34, 32, 62, 32)
                $Graphics.DrawLine($primaryPen, 34, 43, 56, 43)
                $Graphics.FillEllipse($secondaryBrush, 44, 50, 30, 30)
                $Graphics.DrawEllipse($inkPen, 44, 50, 30, 30)
                $Graphics.DrawLine($neutralPen, 51, 65, 67, 65)
                $Graphics.DrawLine($neutralPen, 59, 57, 59, 73)
            }
            'model-compose' {
                foreach ($y in @(23, 59)) {
                    $Graphics.FillRectangle($primaryBrush, 15, $y, 17, 14)
                    $Graphics.DrawRectangle($thinInkPen, 15, $y, 17, 14)
                }
                $Graphics.DrawLine($secondaryPen, 33, 30, 47, 43)
                $Graphics.DrawLine($secondaryPen, 33, 66, 47, 53)
                $Graphics.FillEllipse($accentBrush, 39, 37, 22, 22)
                $Graphics.DrawEllipse($inkPen, 39, 37, 22, 22)
                Draw-Arrow $Graphics $hotPen 61 48 79 48 8
                $Graphics.DrawRectangle($thinInkPen, 69, 25, 13, 46)
                $Graphics.FillRectangle($neutralBrush, 72, 31, 7, 7)
                $Graphics.FillRectangle($neutralBrush, 72, 43, 7, 7)
                $Graphics.FillRectangle($neutralBrush, 72, 55, 7, 7)
            }
            'compile' {
                $Graphics.DrawLines($primaryPen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(38, 22), [System.Drawing.PointF]::new(17, 48),
                    [System.Drawing.PointF]::new(38, 74)))
                $Graphics.DrawLines($secondaryPen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(58, 22), [System.Drawing.PointF]::new(79, 48),
                    [System.Drawing.PointF]::new(58, 74)))
                $Graphics.DrawLine($accentPen, 53, 19, 43, 77)
            }
            { $_ -in @('read', 'read-model', 'read-result') } {
                $Graphics.FillRectangle($neutralBrush, 19, 18, 43, 60)
                $Graphics.DrawRectangle($inkPen, 19, 18, 43, 60)
                if ($Kind -eq 'read-model') {
                    $Graphics.FillRectangle($primaryBrush, 27, 29, 13, 13)
                    $Graphics.FillRectangle($secondaryBrush, 41, 38, 13, 13)
                    $Graphics.DrawRectangle($thinInkPen, 27, 29, 13, 13)
                    $Graphics.DrawRectangle($thinInkPen, 41, 38, 13, 13)
                }
                elseif ($Kind -eq 'read-result') {
                    $Graphics.FillRectangle($primaryBrush, 27, 40, 7, 12)
                    $Graphics.FillRectangle($secondaryBrush, 37, 31, 7, 21)
                    $Graphics.FillRectangle($accentBrush, 47, 23, 7, 29)
                    $Graphics.DrawLine($thinInkPen, 25, 54, 56, 54)
                }
                else {
                    $Graphics.DrawLine($primaryPen, 28, 33, 52, 33)
                    $Graphics.DrawLine($primaryPen, 28, 45, 48, 45)
                }
                Draw-Arrow $Graphics $secondaryPen 80 66 48 66 10
            }
            { $_ -in @('write', 'write-model', 'write-result') } {
                $Graphics.FillRectangle($neutralBrush, 34, 18, 43, 60)
                $Graphics.DrawRectangle($inkPen, 34, 18, 43, 60)
                if ($Kind -eq 'write-model') {
                    $Graphics.FillRectangle($primaryBrush, 42, 29, 13, 13)
                    $Graphics.FillRectangle($secondaryBrush, 56, 38, 13, 13)
                    $Graphics.DrawRectangle($thinInkPen, 42, 29, 13, 13)
                    $Graphics.DrawRectangle($thinInkPen, 56, 38, 13, 13)
                }
                elseif ($Kind -eq 'write-result') {
                    $Graphics.FillRectangle($primaryBrush, 42, 40, 7, 12)
                    $Graphics.FillRectangle($secondaryBrush, 52, 31, 7, 21)
                    $Graphics.FillRectangle($accentBrush, 62, 23, 7, 29)
                    $Graphics.DrawLine($thinInkPen, 40, 54, 71, 54)
                }
                else {
                    $Graphics.DrawLine($primaryPen, 44, 33, 67, 33)
                    $Graphics.DrawLine($primaryPen, 44, 45, 63, 45)
                }
                Draw-Arrow $Graphics $hotPen 48 66 16 66 10
            }
            'extract' {
                $Graphics.FillRectangle($primaryBrush, 16, 28, 38, 42)
                $Graphics.DrawRectangle($inkPen, 16, 28, 38, 42)
                $Graphics.DrawLine($neutralPen, 22, 39, 47, 39)
                Draw-Arrow $Graphics $accentPen 43 58 80 28 11
            }
            'convert' {
                Draw-Arrow $Graphics $primaryPen 17 35 77 35 11
                Draw-Arrow $Graphics $hotPen 79 61 19 61 11
            }
            'batch' {
                $Graphics.FillRectangle($neutralBrush, 17, 18, 48, 60)
                $Graphics.DrawRectangle($inkPen, 17, 18, 48, 60)
                foreach ($y in @(31, 43, 55, 67)) {
                    $Graphics.DrawLine($primaryPen, 25, $y, 52, $y)
                }
                $Graphics.FillPolygon($accentBrush, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(58, 41), [System.Drawing.PointF]::new(58, 75),
                    [System.Drawing.PointF]::new(84, 58)))
                $Graphics.DrawPolygon($inkPen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(58, 41), [System.Drawing.PointF]::new(58, 75),
                    [System.Drawing.PointF]::new(84, 58)))
            }
            'managed-batch' {
                $diamond = [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(48, 10), [System.Drawing.PointF]::new(86, 48),
                    [System.Drawing.PointF]::new(48, 86), [System.Drawing.PointF]::new(10, 48))
                $Graphics.FillPolygon($secondaryBrush, $diamond)
                $Graphics.DrawPolygon($inkPen, $diamond)
                $Graphics.FillRectangle($neutralBrush, 25, 26, 42, 44)
                $Graphics.DrawRectangle($thinInkPen, 25, 26, 42, 44)
                foreach ($y in @(36, 47, 58)) {
                    $Graphics.DrawLine($primaryPen, 31, $y, 48, $y)
                }
                $Graphics.FillPolygon($hotBrush, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(51, 40), [System.Drawing.PointF]::new(51, 62),
                    [System.Drawing.PointF]::new(68, 51)))
                $Graphics.DrawPolygon($thinInkPen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(51, 40), [System.Drawing.PointF]::new(51, 62),
                    [System.Drawing.PointF]::new(68, 51)))
            }
            'profile' {
                $Graphics.FillEllipse($neutralBrush, 37, 16, 22, 22)
                $Graphics.DrawEllipse($inkPen, 37, 16, 22, 22)
                $body = [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(23, 75),
                    [System.Drawing.PointF]::new(28, 51),
                    [System.Drawing.PointF]::new(48, 40),
                    [System.Drawing.PointF]::new(68, 51),
                    [System.Drawing.PointF]::new(73, 75))
                $Graphics.FillPolygon($primaryBrush, $body)
                $Graphics.DrawPolygon($inkPen, $body)
                $Graphics.DrawArc($accentPen, 58, 18, 25, 25, -75, 250)
                Draw-Arrow $Graphics $accentPen 80 28 74 20 5
            }
            'heat-pump' {
                $Graphics.FillEllipse($inkBrush, 20, 18, 56, 60)
                Draw-Arrow $Graphics $primaryPen 24 35 70 35 10
                Draw-Arrow $Graphics $hotPen 72 61 26 61 10
                $Graphics.FillEllipse($neutralBrush, 40, 40, 16, 16)
                $Graphics.DrawEllipse($accentPen, 40, 40, 16, 16)
            }
            'ground' {
                $Graphics.FillRectangle($neutralBrush, 25, 15, 46, 21)
                $Graphics.DrawRectangle($inkPen, 25, 15, 46, 21)
                foreach ($y in @(51, 63, 75)) { $Graphics.DrawLine($secondaryPen, 15, $y, 81, $y) }
                $Graphics.DrawLine($primaryPen, 34, 29, 34, 67)
                $Graphics.DrawArc($primaryPen, 34, 56, 28, 20, 0, 180)
                $Graphics.DrawLine($primaryPen, 62, 29, 62, 67)
            }
            'cooling-tower' {
                $tower = [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(28, 24), [System.Drawing.PointF]::new(68, 24),
                    [System.Drawing.PointF]::new(60, 48), [System.Drawing.PointF]::new(70, 78),
                    [System.Drawing.PointF]::new(26, 78), [System.Drawing.PointF]::new(36, 48))
                $Graphics.FillPolygon($neutralBrush, $tower)
                $Graphics.DrawPolygon($inkPen, $tower)
                $Graphics.DrawArc($primaryPen, 27, 10, 24, 22, 180, 145)
                $Graphics.DrawArc($primaryPen, 46, 7, 25, 24, 180, 150)
                foreach ($point in @(@(39,51), @(50,59), @(58,49))) {
                    $Graphics.FillEllipse($primaryBrush, $point[0] - 4, $point[1] - 5, 8, 11)
                }
            }
            'snowflake' {
                $Graphics.FillEllipse($inkBrush, 16, 16, 64, 64)
                Draw-Snowflake $Graphics $primaryPen 48 48 27
                $Graphics.FillEllipse($neutralBrush, 43, 43, 10, 10)
            }
            'absorption' {
                $Graphics.DrawLine($neutralPen, 48, 19, 48, 77)
                Draw-Snowflake $Graphics $primaryPen 31 45 17
                Draw-Flame $Graphics $hotBrush $accentBrush $inkPen 49 27 29 43
            }
            'flame' {
                $Graphics.FillRectangle($neutralBrush, 21, 18, 54, 61)
                $Graphics.DrawRectangle($inkPen, 21, 18, 54, 61)
                $Graphics.DrawLine($inkPen, 33, 17, 33, 10)
                $Graphics.DrawLine($inkPen, 63, 17, 63, 10)
                Draw-Flame $Graphics $hotBrush $accentBrush $inkPen 32 33 33 40
            }
            'ventilation-link' {
                Draw-Fan $Graphics $primaryBrush $accentBrush $inkPen 27 48 15
                $Graphics.DrawLine($secondaryPen, 43, 48, 63, 48)
                $Graphics.DrawLine($thinInkPen, 63, 25, 63, 71)
                foreach ($y in @(28, 48, 68)) {
                    $Graphics.DrawLine($secondaryPen, 63, $y, 72, $y)
                    $Graphics.FillEllipse($neutralBrush, 70, $y - 6, 12, 12)
                    $Graphics.DrawEllipse($thinInkPen, 70, $y - 6, 12, 12)
                }
            }
            { $_ -in @('network', 'assign', 'assign-air', 'assign-supply') } {
                $flowPen = if ($Kind -eq 'assign-air') {
                    $primaryPen
                } elseif ($Kind -eq 'assign-supply') {
                    $accentPen
                } elseif ($Kind -eq 'network') {
                    $hotPen
                } else {
                    $secondaryPen
                }
                if ($Kind -eq 'network') {
                    $Graphics.DrawLine($flowPen, 25, 64, 48, 25)
                    $Graphics.DrawLine($flowPen, 48, 25, 73, 64)
                    $Graphics.DrawLine($flowPen, 25, 64, 73, 64)
                    foreach ($point in @(@(25,64), @(48,25), @(73,64))) {
                        $Graphics.FillEllipse($neutralBrush, $point[0] - 9, $point[1] - 9, 18, 18)
                        $Graphics.DrawEllipse($inkPen, $point[0] - 9, $point[1] - 9, 18, 18)
                    }
                }
                elseif ($Kind -eq 'assign-air') {
                    Draw-Fan $Graphics $primaryBrush $accentBrush $inkPen 25 48 14
                    $Graphics.DrawBezier($primaryPen, 39, 35, 51, 23, 64, 23, 79, 31)
                    $Graphics.DrawBezier($primaryPen, 39, 58, 51, 70, 64, 70, 79, 63)
                    Draw-Arrow $Graphics $primaryPen 63 27 80 31 7
                    Draw-Arrow $Graphics $primaryPen 63 67 80 63 7
                }
                elseif ($Kind -eq 'assign-supply') {
                    $Graphics.FillRectangle($accentBrush, 13, 35, 24, 28)
                    $Graphics.DrawRectangle($inkPen, 13, 35, 24, 28)
                    $Graphics.DrawLine($accentPen, 37, 49, 53, 49)
                    $Graphics.DrawLine($accentPen, 53, 49, 69, 29)
                    $Graphics.DrawLine($accentPen, 53, 49, 69, 69)
                    foreach ($point in @(@(74,25), @(74,73))) {
                        $Graphics.FillRectangle($secondaryBrush, $point[0] - 9, $point[1] - 9, 18, 18)
                        $Graphics.DrawRectangle($inkPen, $point[0] - 9, $point[1] - 9, 18, 18)
                    }
                }
                else {
                    $Graphics.FillEllipse($neutralBrush, 13, 38, 22, 22)
                    $Graphics.DrawEllipse($inkPen, 13, 38, 22, 22)
                    Draw-Arrow $Graphics $flowPen 34 49 62 31 9
                    Draw-Arrow $Graphics $flowPen 34 49 62 67 9
                    foreach ($point in @(@(69,27), @(69,71))) {
                        $Graphics.FillEllipse($secondaryBrush, $point[0] - 10, $point[1] - 10, 20, 20)
                        $Graphics.DrawEllipse($inkPen, $point[0] - 10, $point[1] - 10, 20, 20)
                    }
                }
            }
            'packaged' {
                $Graphics.FillRectangle($neutralBrush, 18, 17, 60, 62)
                $Graphics.DrawRectangle($inkPen, 18, 17, 60, 62)
                Draw-Fan $Graphics $primaryBrush $accentBrush $inkPen 47 47 22
                $Graphics.DrawLine($secondaryPen, 68, 27, 68, 67)
                foreach ($y in @(32, 43, 54, 65)) { $Graphics.DrawLine($thinInkPen, 65, $y, 73, $y) }
            }
            'ahu' {
                $Graphics.FillRectangle($neutralBrush, 9, 27, 78, 43)
                $Graphics.DrawRectangle($inkPen, 9, 27, 78, 43)
                $Graphics.DrawLine($inkPen, 32, 28, 32, 69)
                $Graphics.DrawLine($inkPen, 58, 28, 58, 69)
                $Graphics.DrawLine($primaryPen, 16, 61, 28, 35)
                Draw-Fan $Graphics $secondaryBrush $accentBrush $thinInkPen 46 48 10
                Draw-Arrow $Graphics $hotPen 62 48 82 48 7
            }
            'coil' {
                Draw-Fan $Graphics $secondaryBrush $accentBrush $inkPen 29 48 20
                foreach ($x in @(49, 60, 71)) {
                    $Graphics.DrawArc($primaryPen, $x, 25, 14, 46, -90, 180)
                }
            }
            'radiator' {
                $Graphics.FillRectangle($neutralBrush, 17, 24, 62, 48)
                $Graphics.DrawRectangle($inkPen, 17, 24, 62, 48)
                foreach ($x in @(26, 37, 48, 59, 70)) {
                    $Graphics.DrawLine($hotPen, $x, 31, $x, 65)
                }
                $Graphics.DrawLine($inkPen, 26, 72, 26, 81)
                $Graphics.DrawLine($inkPen, 70, 72, 70, 81)
            }
            'electric-radiator' {
                $Graphics.FillRectangle($primaryBrush, 23, 17, 50, 62)
                $Graphics.DrawRectangle($inkPen, 23, 17, 50, 62)
                foreach ($y in @(29, 43, 57, 71)) { $Graphics.DrawLine($neutralPen, 29, $y, 67, $y) }
                Draw-Lightning $Graphics $accentPen 34 21 31 53
            }
            'radiant-floor' {
                $floor = [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(14, 43), [System.Drawing.PointF]::new(65, 23),
                    [System.Drawing.PointF]::new(83, 53), [System.Drawing.PointF]::new(31, 76))
                $Graphics.FillPolygon($neutralBrush, $floor)
                $Graphics.DrawPolygon($inkPen, $floor)
                $Graphics.DrawLines($hotPen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(23, 48), [System.Drawing.PointF]::new(61, 33),
                    [System.Drawing.PointF]::new(70, 42), [System.Drawing.PointF]::new(31, 58),
                    [System.Drawing.PointF]::new(39, 68), [System.Drawing.PointF]::new(77, 51)))
            }
            'electric-floor' {
                $Graphics.FillPolygon($primaryBrush, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(13, 39), [System.Drawing.PointF]::new(65, 20),
                    [System.Drawing.PointF]::new(84, 57), [System.Drawing.PointF]::new(32, 78)))
                $Graphics.DrawLines($neutralPen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(20, 46), [System.Drawing.PointF]::new(34, 50),
                    [System.Drawing.PointF]::new(43, 40), [System.Drawing.PointF]::new(55, 45),
                    [System.Drawing.PointF]::new(66, 34), [System.Drawing.PointF]::new(77, 43)))
                Draw-Lightning $Graphics $accentPen 37 20 28 50
            }
            'erv' {
                $Graphics.FillPolygon($neutralBrush, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(48, 29), [System.Drawing.PointF]::new(66, 48),
                    [System.Drawing.PointF]::new(48, 67), [System.Drawing.PointF]::new(30, 48)))
                $Graphics.DrawPolygon($inkPen, [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(48, 29), [System.Drawing.PointF]::new(66, 48),
                    [System.Drawing.PointF]::new(48, 67), [System.Drawing.PointF]::new(30, 48)))
                Draw-Arrow $Graphics $primaryPen 14 25 80 68 12
                Draw-Arrow $Graphics $hotPen 82 27 16 70 12
            }
            'photovoltaic' {
                $panel = [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(15, 35), [System.Drawing.PointF]::new(64, 24),
                    [System.Drawing.PointF]::new(77, 67), [System.Drawing.PointF]::new(28, 77))
                $Graphics.FillPolygon($primaryBrush, $panel)
                $Graphics.DrawPolygon($inkPen, $panel)
                $Graphics.DrawLine($neutralPen, 39, 30, 52, 72)
                $Graphics.DrawLine($neutralPen, 21, 49, 70, 38)
                $Graphics.DrawLine($neutralPen, 25, 63, 74, 52)
                $Graphics.FillEllipse($accentBrush, 66, 10, 20, 20)
                $Graphics.DrawEllipse($inkPen, 66, 10, 20, 20)
            }
            default { throw "Unknown icon overlay '$Kind'." }
        }
    }
    finally {
        $thinInkPen.Dispose()
        $inkPen.Dispose()
        $neutralPen.Dispose()
        $hotPen.Dispose()
        $accentPen.Dispose()
        $secondaryPen.Dispose()
        $primaryPen.Dispose()
        $inkBrush.Dispose()
        $neutralBrush.Dispose()
        $hotBrush.Dispose()
        $accentBrush.Dispose()
        $secondaryBrush.Dispose()
        $primaryBrush.Dispose()
    }
}

function Draw-ParameterFrame {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Graphics] $Graphics,
        [Parameter(Mandatory = $true)] [object] $Palette
    )

    $framePen = New-RoundPen $Palette.Border 5.0
    $socketPen = New-RoundPen $Palette.Ink 3.5
    $socketBrush = [System.Drawing.SolidBrush]::new($Palette.Neutral)
    try {
        foreach ($points in @(
            @(@(29, 10), @(10, 10), @(10, 29)),
            @(@(67, 10), @(86, 10), @(86, 29)),
            @(@(10, 67), @(10, 86), @(29, 86)),
            @(@(86, 67), @(86, 86), @(67, 86)))) {
            $Graphics.DrawLines($framePen, [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($points[0][0], $points[0][1]),
                [System.Drawing.PointF]::new($points[1][0], $points[1][1]),
                [System.Drawing.PointF]::new($points[2][0], $points[2][1])))
        }

        foreach ($x in @(5, 77)) {
            $Graphics.FillEllipse($socketBrush, $x, 41, 14, 14)
            $Graphics.DrawEllipse($socketPen, $x, 41, 14, 14)
        }
    }
    finally {
        $socketBrush.Dispose()
        $socketPen.Dispose()
        $framePen.Dispose()
    }
}

function Write-ComponentPng {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Bitmap] $Atlas,
        [Parameter(Mandatory = $true)] [ValidateRange(0, 15)] [int] $Tile,
        [Parameter(Mandatory = $true)] [string] $Overlay,
        [Parameter(Mandatory = $true)] [object] $Palette,
        [Parameter(Mandatory = $true)] [string] $Destination,
        [System.Drawing.Bitmap] $Illustration,
        [ValidateSet('component', 'parameter')] [string] $Role = 'component'
    )

    $sourceBitmap = if ($null -ne $Illustration) { $Illustration } else { $Atlas }
    $source = if ($null -ne $Illustration) {
        Get-OpaqueBounds $Illustration `
            ([System.Drawing.Rectangle]::new(0, 0, $Illustration.Width, $Illustration.Height))
    }
    else {
        Get-OpaqueBounds $Atlas (Get-AtlasCell $Atlas $Tile)
    }
    $maximum = if ($Role -eq 'parameter') { 64.0 } else { 76.0 }
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
            Set-HighQualityDrawing $graphics
            if ($Role -eq 'parameter') {
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
                Draw-FunctionalBackplate $graphics $Palette
            }
            else {
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            }
            Draw-ImageRegion $graphics $sourceBitmap $destinationRectangle $source
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
            Draw-Overlay $graphics $Overlay $Palette
            if ($Role -eq 'parameter') {
                Draw-ParameterFrame $graphics $Palette
            }
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
        [Parameter(Mandatory = $true)] [string] $Destination,
        [ValidateSet('component', 'parameter')] [string] $Role = 'component'
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
                $graphics.DrawString("$ProductSlug $Role icons", $headingFont, $headingBrush, 12, 10)
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
                        ($component.Name -replace '(Component|Param)$', ''),
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

function New-IconVisualSample {
    param([Parameter(Mandatory = $true)] [System.Drawing.Bitmap] $Bitmap)

    $pixelCount = $Bitmap.Width * $Bitmap.Height
    $argb = [int[]]::new($pixelCount)
    $light = [double[]]::new($pixelCount * 3)
    $dark = [double[]]::new($pixelCount * 3)
    $lightBackground = @(242.0, 244.0, 247.0)
    $darkBackground = @(45.0, 49.0, 57.0)
    $index = 0
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            $color = $Bitmap.GetPixel($x, $y)
            $argb[$index] = $color.ToArgb()
            $alpha = $color.A / 255.0
            $inverseAlpha = 1.0 - $alpha
            $channels = @([double] $color.R, [double] $color.G, [double] $color.B)
            $channelOffset = $index * 3
            for ($channel = 0; $channel -lt 3; $channel++) {
                $light[$channelOffset + $channel] =
                    $channels[$channel] * $alpha + $lightBackground[$channel] * $inverseAlpha
                $dark[$channelOffset + $channel] =
                    $channels[$channel] * $alpha + $darkBackground[$channel] * $inverseAlpha
            }
            $index++
        }
    }

    return [pscustomobject]@{
        Argb = $argb
        Light = $light
        Dark = $dark
    }
}

function Measure-IconVisualSeparation {
    param(
        [Parameter(Mandatory = $true)] [object] $Left,
        [Parameter(Mandatory = $true)] [object] $Right
    )

    $identical = 0
    for ($index = 0; $index -lt $Left.Argb.Length; $index++) {
        if ($Left.Argb[$index] -eq $Right.Argb[$index]) {
            $identical++
        }
    }

    $lightSquared = 0.0
    $darkSquared = 0.0
    for ($index = 0; $index -lt $Left.Light.Length; $index++) {
        $lightDelta = $Left.Light[$index] - $Right.Light[$index]
        $darkDelta = $Left.Dark[$index] - $Right.Dark[$index]
        $lightSquared += $lightDelta * $lightDelta
        $darkSquared += $darkDelta * $darkDelta
    }

    $lightDistance = [Math]::Sqrt($lightSquared / $Left.Light.Length) / 255.0
    $darkDistance = [Math]::Sqrt($darkSquared / $Left.Dark.Length) / 255.0
    return [pscustomobject]@{
        IdenticalPixelRatio = $identical / [double] $Left.Argb.Length
        PerceptualDistance = [Math]::Min($lightDistance, $darkDistance)
    }
}

function Assert-IconVisualSeparation {
    param(
        [Parameter(Mandatory = $true)] [object[]] $Samples,
        [Parameter(Mandatory = $true)] [string] $Scope
    )

    # These thresholds deliberately reject the old lower-right-badge system,
    # whose confusing families shared 82-91% of their pixels and reached a
    # normalized light/dark RMS distance as low as 0.045.
    $maximumIdenticalPixelRatio = 0.72
    $minimumPerceptualDistance = 0.10
    $maximumIdentical = [pscustomobject]@{ Value = -1.0; Left = ''; Right = '' }
    $minimumDistance = [pscustomobject]@{ Value = [double]::PositiveInfinity; Left = ''; Right = '' }
    for ($leftIndex = 0; $leftIndex -lt $Samples.Count; $leftIndex++) {
        for ($rightIndex = $leftIndex + 1; $rightIndex -lt $Samples.Count; $rightIndex++) {
            $left = $Samples[$leftIndex]
            $right = $Samples[$rightIndex]
            $separation = Measure-IconVisualSeparation $left.Sample $right.Sample
            if ($separation.IdenticalPixelRatio -gt $maximumIdentical.Value) {
                $maximumIdentical = [pscustomobject]@{
                    Value = [double] $separation.IdenticalPixelRatio
                    Left = $left.Name
                    Right = $right.Name
                }
            }
            if ($separation.PerceptualDistance -lt $minimumDistance.Value) {
                $minimumDistance = [pscustomobject]@{
                    Value = [double] $separation.PerceptualDistance
                    Left = $left.Name
                    Right = $right.Name
                }
            }
        }
    }

    if ($maximumIdentical.Value -gt $maximumIdenticalPixelRatio) {
        throw ("Icons are too pixel-similar in {0}: '{1}' and '{2}' share {3:P1}; maximum is {4:P0}." -f
            $Scope,
            $maximumIdentical.Left,
            $maximumIdentical.Right,
            $maximumIdentical.Value,
            $maximumIdenticalPixelRatio)
    }
    if ($minimumDistance.Value -lt $minimumPerceptualDistance) {
        throw ("Icons are not perceptually separated in {0}: '{1}' and '{2}' have distance {3:F3}; minimum is {4:F2}." -f
            $Scope,
            $minimumDistance.Left,
            $minimumDistance.Right,
            $minimumDistance.Value,
            $minimumPerceptualDistance)
    }

    Write-Host ("Icon separation ({0}): maximum identical pixels {1:P1} ({2} / {3}); minimum perceptual distance {4:F3} ({5} / {6})." -f
        $Scope,
        $maximumIdentical.Value,
        $maximumIdentical.Left,
        $maximumIdentical.Right,
        $minimumDistance.Value,
        $minimumDistance.Left,
        $minimumDistance.Right)
}

function Assert-GeneratedIcons {
    param(
        [Parameter(Mandatory = $true)] [object[]] $Icons,
        [Parameter(Mandatory = $true)] [string] $IconDirectory,
        [Parameter(Mandatory = $true)] [string] $Scope,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)] [System.Collections.Generic.List[object]] $AllVisualSamples
    )

    $hashes = @{}
    $productVisualSamples = [System.Collections.Generic.List[object]]::new()
    foreach ($icon in $Icons) {
        $path = Join-Path $IconDirectory "$($icon.Name).png"
        $bitmap = [System.Drawing.Bitmap]::new($path)
        try {
            if ($bitmap.Width -ne 24 -or $bitmap.Height -ne 24) {
                throw "Generated icon must be exactly 24x24: $path"
            }

            for ($pixel = 0; $pixel -lt 24; $pixel++) {
                foreach ($edge in @(0, 1, 22, 23)) {
                    if ($bitmap.GetPixel($edge, $pixel).A -ne 0 -or $bitmap.GetPixel($pixel, $edge).A -ne 0) {
                        throw "Generated icon must preserve a two-pixel transparent border: $path"
                    }
                }
            }
            $visualSample = [pscustomobject]@{
                Name = "$Scope/$($icon.Name)"
                Sample = New-IconVisualSample $bitmap
            }
            $productVisualSamples.Add($visualSample)
            $AllVisualSamples.Add($visualSample)
        }
        finally { $bitmap.Dispose() }

        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($hashes.ContainsKey($hash)) {
            throw "Generated icons must not be byte-identical: '$($hashes[$hash])' and '$($icon.Name)'."
        }
        $hashes[$hash] = $icon.Name
    }

    Assert-IconVisualSeparation `
        -Samples $productVisualSamples.ToArray() `
        -Scope $Scope
}

$allVisualSamples = [System.Collections.Generic.List[object]]::new()
foreach ($product in $products) {
    foreach ($requiredFile in @($product.Source, $product.Atlas)) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Icon source does not exist: $requiredFile"
        }
    }

    $duplicateNames = @(
        @($product.Components) + @($product.Parameters) |
            Group-Object Name |
            Where-Object Count -ne 1)
    if ($duplicateNames.Count -ne 0) {
        throw "Duplicate icon names for $($product.Slug): $($duplicateNames.Name -join ', ')"
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
            $illustration = $null
            try {
                if (-not [string]::IsNullOrWhiteSpace($component.Illustration)) {
                    if (-not (Test-Path -LiteralPath $component.Illustration -PathType Leaf)) {
                        throw "Component illustration does not exist: $($component.Illustration)"
                    }
                    $illustration = [System.Drawing.Bitmap]::new($component.Illustration)
                }
                Write-ComponentPng `
                    $atlas `
                    $component.Tile `
                    $component.Overlay `
                    $product.Palette `
                    $destination `
                    -Illustration $illustration
            }
            finally {
                if ($null -ne $illustration) { $illustration.Dispose() }
            }
            Write-Host "Generated $destination"
        }
        foreach ($existing in [System.IO.Directory]::GetFiles($componentDirectory, '*.png')) {
            if (-not $expectedPaths.Contains([System.IO.Path]::GetFullPath($existing))) {
                Remove-Item -LiteralPath $existing -Force
                Write-Host "Removed stale component icon $existing"
            }
        }

        Assert-GeneratedIcons `
            -Icons $product.Components `
            -IconDirectory $componentDirectory `
            -Scope "$($product.Slug)/components" `
            -AllVisualSamples $allVisualSamples
        $contactSheet = Join-Path $generatedDirectory "$($product.Slug)-component-contact-sheet.png"
        Write-ContactSheet $product.Slug $product.Components $componentDirectory $contactSheet
        Write-Host "Generated $contactSheet"

        $parameterDirectory = Join-Path $generatedDirectory 'parameters'
        [System.IO.Directory]::CreateDirectory($parameterDirectory) | Out-Null
        $expectedParameterPaths = [System.Collections.Generic.HashSet[string]]::new(
            [System.StringComparer]::OrdinalIgnoreCase)
        foreach ($parameter in $product.Parameters) {
            $destination = Join-Path $parameterDirectory "$($parameter.Name).png"
            [void]$expectedParameterPaths.Add([System.IO.Path]::GetFullPath($destination))
            $illustration = $null
            try {
                if (-not [string]::IsNullOrWhiteSpace($parameter.Illustration)) {
                    if (-not (Test-Path -LiteralPath $parameter.Illustration -PathType Leaf)) {
                        throw "Parameter illustration does not exist: $($parameter.Illustration)"
                    }
                    $illustration = [System.Drawing.Bitmap]::new($parameter.Illustration)
                }
                Write-ComponentPng `
                    $atlas `
                    $parameter.Tile `
                    $parameter.Overlay `
                    $product.Palette `
                    $destination `
                    -Illustration $illustration `
                    -Role parameter
            }
            finally {
                if ($null -ne $illustration) { $illustration.Dispose() }
            }
            Write-Host "Generated $destination"
        }
        foreach ($existing in [System.IO.Directory]::GetFiles($parameterDirectory, '*.png')) {
            if (-not $expectedParameterPaths.Contains([System.IO.Path]::GetFullPath($existing))) {
                Remove-Item -LiteralPath $existing -Force
                Write-Host "Removed stale parameter icon $existing"
            }
        }

        Assert-GeneratedIcons `
            -Icons $product.Parameters `
            -IconDirectory $parameterDirectory `
            -Scope "$($product.Slug)/parameters" `
            -AllVisualSamples $allVisualSamples
        $parameterContactSheet =
            Join-Path $generatedDirectory "$($product.Slug)-parameter-contact-sheet.png"
        Write-ContactSheet `
            $product.Slug `
            $product.Parameters `
            $parameterDirectory `
            $parameterContactSheet `
            -Role parameter
        Write-Host "Generated $parameterContactSheet"
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

Assert-IconVisualSeparation `
    -Samples $allVisualSamples.ToArray() `
    -Scope 'all products'

namespace GonieGonie.SimpleDragon.Internal;

internal static class GrmVocabulary
{
    public static readonly SourceSystemType[] SourceSystemOrder =
    {
        SourceSystemType.HeatPump,
        SourceSystemType.GeothermalHeatPump,
        SourceSystemType.Chiller,
        SourceSystemType.AbsorptionChiller,
        SourceSystemType.Boiler,
        SourceSystemType.DistrictHeating,
    };

    public static readonly SupplySystemType[] SupplySystemOrder =
    {
        SupplySystemType.PackagedAirConditioner,
        SupplySystemType.AirHandlingUnit,
        SupplySystemType.FanCoilUnit,
        SupplySystemType.Radiator,
        SupplySystemType.ElectricRadiator,
        SupplySystemType.RadiantFloor,
        SupplySystemType.ElectricRadiantFloor,
    };

    public static string ToGrm(SourceSystemType value)
    {
        return value switch
        {
            SourceSystemType.HeatPump => "heatpump",
            SourceSystemType.GeothermalHeatPump => "geothermal_heatpump",
            SourceSystemType.Chiller => "chiller",
            SourceSystemType.AbsorptionChiller => "absorption_chiller",
            SourceSystemType.Boiler => "boiler",
            SourceSystemType.DistrictHeating => "district_heating",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown source-system type."),
        };
    }

    public static SourceSystemType ParseSourceSystemType(string value)
    {
        return value switch
        {
            "heatpump" => SourceSystemType.HeatPump,
            "geothermal_heatpump" => SourceSystemType.GeothermalHeatPump,
            "chiller" => SourceSystemType.Chiller,
            "absorption_chiller" => SourceSystemType.AbsorptionChiller,
            "boiler" => SourceSystemType.Boiler,
            "district_heating" => SourceSystemType.DistrictHeating,
            _ => throw new ArgumentException("Unknown GRM source-system type '" + value + "'.", nameof(value)),
        };
    }

    public static string ToGrm(SupplySystemType value)
    {
        return value switch
        {
            SupplySystemType.PackagedAirConditioner => "packaged_air_conditioner",
            SupplySystemType.AirHandlingUnit => "air_handling_unit",
            SupplySystemType.FanCoilUnit => "fan_coil_unit",
            SupplySystemType.Radiator => "radiator",
            SupplySystemType.ElectricRadiator => "electric_radiator",
            SupplySystemType.RadiantFloor => "radiant_floor",
            SupplySystemType.ElectricRadiantFloor => "electric_radiant_floor",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown supply-system type."),
        };
    }

    public static SupplySystemType ParseSupplySystemType(string value)
    {
        return value switch
        {
            "packaged_air_conditioner" => SupplySystemType.PackagedAirConditioner,
            "air_handling_unit" => SupplySystemType.AirHandlingUnit,
            "fan_coil_unit" => SupplySystemType.FanCoilUnit,
            "radiator" => SupplySystemType.Radiator,
            "electric_radiator" => SupplySystemType.ElectricRadiator,
            "radiant_floor" => SupplySystemType.RadiantFloor,
            "electric_radiant_floor" => SupplySystemType.ElectricRadiantFloor,
            _ => throw new ArgumentException("Unknown GRM supply-system type '" + value + "'.", nameof(value)),
        };
    }

    public static string ToGrm(FuelType value)
    {
        return value switch
        {
            FuelType.Electricity => "electricity",
            FuelType.NaturalGas => "natural_gas",
            FuelType.LiquefiedPetroleumGas => "lpg",
            FuelType.Oil => "oil",
            FuelType.DistrictHeating => "district_heating",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown fuel type."),
        };
    }

    public static FuelType ParseFuel(string value)
    {
        return value switch
        {
            "electricity" => FuelType.Electricity,
            "natural_gas" => FuelType.NaturalGas,
            "lpg" => FuelType.LiquefiedPetroleumGas,
            "oil" => FuelType.Oil,
            "district_heating" => FuelType.DistrictHeating,
            _ => throw new ArgumentException("Unknown GRM fuel type '" + value + "'.", nameof(value)),
        };
    }

    public static string ToGrm(SurfaceType value)
    {
        return value switch
        {
            SurfaceType.Wall => "wall",
            SurfaceType.Ceiling => "ceiling",
            SurfaceType.Floor => "floor",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown surface type."),
        };
    }

    public static SurfaceType ParseSurfaceType(string value)
    {
        return value switch
        {
            "wall" => SurfaceType.Wall,
            "ceiling" => SurfaceType.Ceiling,
            "floor" => SurfaceType.Floor,
            _ => throw new ArgumentException("Unknown GRM surface type '" + value + "'.", nameof(value)),
        };
    }

    public static string ToGrm(SurfaceBoundaryCondition value)
    {
        return value switch
        {
            SurfaceBoundaryCondition.Outdoors => "outdoors",
            SurfaceBoundaryCondition.Ground => "ground",
            SurfaceBoundaryCondition.AdjacentSpace => "zone",
            SurfaceBoundaryCondition.Adiabatic => "adiabatic",
            SurfaceBoundaryCondition.Zone => "zone",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown boundary condition."),
        };
    }

    public static SurfaceBoundaryCondition ParseBoundary(string value)
    {
        return value switch
        {
            "outdoors" => SurfaceBoundaryCondition.Outdoors,
            "ground" => SurfaceBoundaryCondition.Ground,
            "adiabatic" => SurfaceBoundaryCondition.Adiabatic,
            "zone" => SurfaceBoundaryCondition.Zone,
            _ => throw new ArgumentException("Unknown GRM boundary condition '" + value + "'.", nameof(value)),
        };
    }

    public static string ToGrm(FenestrationType value)
    {
        return value switch
        {
            FenestrationType.Window => "window",
            FenestrationType.Door => "door",
            FenestrationType.GlassDoor => "glassdoor",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown fenestration type."),
        };
    }

    public static FenestrationType ParseFenestrationType(string value)
    {
        return value switch
        {
            "window" => FenestrationType.Window,
            "door" => FenestrationType.Door,
            "glassdoor" => FenestrationType.GlassDoor,
            _ => throw new ArgumentException("Unknown GRM fenestration type '" + value + "'.", nameof(value)),
        };
    }

    public static string ToGrm(BlindType value)
    {
        return value == BlindType.Shade ? "shade" : "venetian";
    }

    public static BlindType ParseBlind(string value)
    {
        return value switch
        {
            "shade" => BlindType.Shade,
            "venetian" => BlindType.Venetian,
            _ => throw new ArgumentException("Unknown GRM blind type '" + value + "'.", nameof(value)),
        };
    }

    public static string ToGrm(CompressorType value)
    {
        return value switch
        {
            CompressorType.Turbo => "turbo",
            CompressorType.Screw => "screw",
            CompressorType.Reciprocating => "reciprocating",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown compressor type."),
        };
    }

    public static CompressorType ParseCompressor(string value)
    {
        return value switch
        {
            "turbo" => CompressorType.Turbo,
            "screw" => CompressorType.Screw,
            "reciprocating" => CompressorType.Reciprocating,
            _ => throw new ArgumentException("Unknown compressor type '" + value + "'.", nameof(value)),
        };
    }

    public static string ToGrm(CoolingTowerType value)
    {
        return value == CoolingTowerType.Open ? "open" : "closed";
    }

    public static CoolingTowerType ParseCoolingTower(string value)
    {
        return value switch
        {
            "open" => CoolingTowerType.Open,
            "closed" => CoolingTowerType.Closed,
            _ => throw new ArgumentException("Unknown cooling-tower type '" + value + "'.", nameof(value)),
        };
    }

    public static string ToGrm(CoolingTowerControl value)
    {
        return value == CoolingTowerControl.SingleSpeed ? "single-speed" : "two-speed";
    }

    public static CoolingTowerControl ParseCoolingTowerControl(string value)
    {
        return value switch
        {
            "single-speed" => CoolingTowerControl.SingleSpeed,
            "two-speed" => CoolingTowerControl.TwoSpeed,
            _ => throw new ArgumentException("Unknown cooling-tower control '" + value + "'.", nameof(value)),
        };
    }
}

using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

public abstract class SimpleDragonHvacComponent : SimpleDragonComponent
{
    protected SimpleDragonHvacComponent(string name, string nickname, string description)
        : base(name, nickname, description, SimpleDragonPanels.Model)
    {
    }

    protected static int AddFuelParameter(
        GH_InputParamManager manager,
        string description,
        FuelType defaultValue)
    {
        int index = manager.AddIntegerParameter(
            "Fuel",
            "Fuel",
            description + " Named values: Electricity, Natural Gas, LPG, Oil, District Heating.",
            GH_ParamAccess.item,
            (int)defaultValue);
        var parameter = (Param_Integer)manager[index];
        parameter.AddNamedValue("Electricity", (int)FuelType.Electricity);
        parameter.AddNamedValue("Natural Gas", (int)FuelType.NaturalGas);
        parameter.AddNamedValue("LPG", (int)FuelType.LiquefiedPetroleumGas);
        parameter.AddNamedValue("Oil", (int)FuelType.Oil);
        parameter.AddNamedValue("District Heating", (int)FuelType.DistrictHeating);
        return index;
    }

    protected static int AddEnumParameter<TEnum>(
        GH_InputParamManager manager,
        string name,
        string nickname,
        string description,
        TEnum defaultValue)
        where TEnum : struct, Enum
    {
        int index = manager.AddIntegerParameter(
            name,
            nickname,
            description,
            GH_ParamAccess.item,
            Convert.ToInt32(defaultValue, System.Globalization.CultureInfo.InvariantCulture));
        var parameter = (Param_Integer)manager[index];
        foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
        {
            parameter.AddNamedValue(
                Enum.GetName(typeof(TEnum), value) ?? value.ToString(),
                Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
        }

        return index;
    }

    protected static TEnum EnumValue<TEnum>(int value, string inputName)
        where TEnum : struct, Enum
    {
        if (Enum.IsDefined(typeof(TEnum), value))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), value);
        }

        string allowed = string.Join(
            ", ",
            Enum.GetNames(typeof(TEnum)).Select((name, index) => name + "=" + index));
        throw new ArgumentOutOfRangeException(
            inputName,
            value,
            inputName + " must be one of " + allowed + ".");
    }

    protected static double? OptionalNumber(IGH_DataAccess access, int index)
    {
        double value = 0;
        return access.GetData(index, ref value) ? value : null;
    }

    protected static T Value<TGoo, T>(TGoo? goo, string inputName)
        where TGoo : SimpleDragonGoo<T>
        where T : class
    {
        return goo?.Value
            ?? throw new ArgumentException(inputName + " contains no value.", inputName);
    }

    protected static void EnsureCompatible(SupplySystemType supplyType, SourceSystem source)
    {
        SourceSystemType[] allowed = supplyType switch
        {
            SupplySystemType.AirHandlingUnit => new[]
            {
                SourceSystemType.HeatPump,
                SourceSystemType.GeothermalHeatPump,
            },
            SupplySystemType.FanCoilUnit => new[]
            {
                SourceSystemType.Boiler,
                SourceSystemType.DistrictHeating,
                SourceSystemType.Chiller,
                SourceSystemType.AbsorptionChiller,
            },
            SupplySystemType.Radiator or SupplySystemType.RadiantFloor => new[]
            {
                SourceSystemType.Boiler,
                SourceSystemType.DistrictHeating,
            },
            _ => Array.Empty<SourceSystemType>(),
        };
        if (allowed.Contains(source.Type))
        {
            return;
        }

        throw new ArgumentException(
            supplyType + " cannot use a " + source.Type + " source. Allowed source types: "
            + string.Join(", ", allowed) + ".",
            nameof(source));
    }

    protected void Author(
        IGH_DataAccess access,
        int diagnosticOutputIndex,
        string code,
        string action,
        Action create)
    {
        try
        {
            create();
            access.SetDataList(diagnosticOutputIndex, Array.Empty<SimpleDragonDiagnosticGoo>());
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is InvalidOperationException)
        {
            var diagnostic = new Diagnostic(
                code,
                DiagnosticSeverity.Error,
                exception.Message,
                suggestedAction: action);
            Report(new[] { diagnostic });
            access.SetDataList(diagnosticOutputIndex, new[] { new SimpleDragonDiagnosticGoo(diagnostic) });
        }
    }
}

using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

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
        return ChoiceInputs.AddEnum(
            manager,
            "Fuel",
            "Fuel",
            description,
            defaultValue);
    }

    protected static int AddEnumParameter<TEnum>(
        GH_InputParamManager manager,
        string name,
        string nickname,
        string description,
        TEnum defaultValue)
        where TEnum : struct, Enum
    {
        return ChoiceInputs.AddEnum(
            manager,
            name,
            nickname,
            description,
            defaultValue);
    }

    protected static TEnum EnumValue<TEnum>(string value, string inputName)
        where TEnum : struct, Enum
    {
        return ChoiceInputs.ParseEnum<TEnum>(value, inputName);
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

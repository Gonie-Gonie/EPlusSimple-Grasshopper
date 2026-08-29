using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Grasshopper.Types;

/// <summary>
/// Builds execution documents with the EnergyPlus 24.2 field positions that
/// differ from the preserved Python-oracle layout.
/// </summary>
public static class EnergyPlus242ExecutionIdfBuilder
{
    private const string EnergyPlusIddSha256 =
        "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2";

    /// <summary>
    /// Gets the embedded, path-free positioning schema used when the full
    /// managed EnergyPlus runtime has not been materialized yet.
    /// </summary>
    public static IddSchema PositioningSchema { get; } = CreateSchema();

    /// <summary>
    /// Creates a detached IDF document using the embedded EnergyPlus 24.2
    /// execution positions.
    /// </summary>
    public static IdfDocument Create(EnergyModel model, EnergyModelIdfOptions options)
    {
#if NETFRAMEWORK
        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }
#else
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(options);
#endif

        IdfDocument positioned = model.ToIdfDocument(PositioningSchema, options);
        return DetachPositioningSchema(positioned);
    }

    private static IddSchema CreateSchema()
    {
        return new IddSchema(
            "24.2.0",
            "94a887817b",
            EnergyPlusIddSha256,
            new[]
            {
                Definition(
                    "AirConditioner:VariableRefrigerantFlow",
                    81,
                    Field(6, "Cooling Capacity Ratio Modifier Function of Low Temperature Curve Name"),
                    Field(7, "Cooling Capacity Ratio Boundary Curve Name"),
                    Field(8, "Cooling Capacity Ratio Modifier Function of High Temperature Curve Name"),
                    Field(9, "Cooling Energy Input Ratio Modifier Function of Low Temperature Curve Name"),
                    Field(10, "Cooling Energy Input Ratio Boundary Curve Name"),
                    Field(11, "Cooling Energy Input Ratio Modifier Function of High Temperature Curve Name"),
                    Field(12, "Cooling Energy Input Ratio Modifier Function of Low Part-Load Ratio Curve Name"),
                    Field(13, "Cooling Energy Input Ratio Modifier Function of High Part-Load Ratio Curve Name"),
                    Field(14, "Cooling Combination Ratio Correction Factor Curve Name"),
                    Field(15, "Cooling Part-Load Fraction Correlation Curve Name"),
                    Field(16, "Gross Rated Heating Capacity"),
                    Field(17, "Rated Heating Capacity Sizing Ratio"),
                    Field(18, "Gross Rated Heating COP"),
                    Field(21, "Heating Capacity Ratio Modifier Function of Low Temperature Curve Name"),
                    Field(22, "Heating Capacity Ratio Boundary Curve Name"),
                    Field(23, "Heating Capacity Ratio Modifier Function of High Temperature Curve Name"),
                    Field(24, "Heating Energy Input Ratio Modifier Function of Low Temperature Curve Name"),
                    Field(25, "Heating Energy Input Ratio Boundary Curve Name"),
                    Field(26, "Heating Energy Input Ratio Modifier Function of High Temperature Curve Name"),
                    Field(28, "Heating Energy Input Ratio Modifier Function of Low Part-Load Ratio Curve Name"),
                    Field(29, "Heating Energy Input Ratio Modifier Function of High Part-Load Ratio Curve Name"),
                    Field(30, "Heating Combination Ratio Correction Factor Curve Name"),
                    Field(31, "Heating Part-Load Fraction Correlation Curve Name"),
                    Field(34, "Master Thermostat Priority Control Type"),
                    Field(36, "Zone Terminal Unit List Name"),
                    Field(66, "Fuel Type")),
                Definition(
                    "ZoneHVAC:TerminalUnit:VariableRefrigerantFlow",
                    33,
                    Field(11, "Supply Air Fan Operating Mode Schedule Name"),
                    Field(12, "Supply Air Fan Placement"),
                    Field(13, "Supply Air Fan Object Type", "Fan:ConstantVolume"),
                    Field(14, "Supply Air Fan Object Name"),
                    Field(17, "Cooling Coil Object Type"),
                    Field(18, "Cooling Coil Object Name"),
                    Field(19, "Heating Coil Object Type"),
                    Field(20, "Heating Coil Object Name"),
                    Field(21, "Zone Terminal Unit On Parasitic Electric Energy Use"),
                    Field(22, "Zone Terminal Unit Off Parasitic Electric Energy Use")),
                Definition(
                    "Fan:ConstantVolume",
                    10,
                    Field(5, "Motor Efficiency"),
                    Field(7, "Air Inlet Node Name"),
                    Field(8, "Air Outlet Node Name")),
                Definition(
                    "ZoneHVAC:LowTemperatureRadiant:VariableFlow",
                    16,
                    Field(6, "Heating Design Capacity", "autosize"),
                    Field(7, "Maximum Hot Water Flow"),
                    Field(8, "Heating Water Inlet Node Name"),
                    Field(9, "Heating Water Outlet Node Name")),
                Definition(
                    "ZoneHVAC:LowTemperatureRadiant:VariableFlow:Design",
                    20,
                    Field(6, "Setpoint Control Type"),
                    Field(7, "Heating Design Capacity Method"),
                    Field(10, "Heating Control Throttling Range"),
                    Field(11, "Heating Control Temperature Schedule Name")),
                Definition(
                    "Sizing:Zone",
                    37,
                    Field(22, "Design Specification Zone Air Distribution Object Name"),
                    Field(36, "Type of Space Sum to Use")),
            });
    }

    private static IddObjectDefinition Definition(
        string name,
        int fieldCount,
        params ExecutionField[] mappedFields)
    {
        Dictionary<int, ExecutionField> mapped = mappedFields.ToDictionary(item => item.Position);
        var fields = new IddFieldDefinition[fieldCount];
        for (int position = 0; position < fields.Length; position++)
        {
            mapped.TryGetValue(position, out ExecutionField? executionField);
            fields[position] = new IddFieldDefinition(
                "A" + (position + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                position,
                IddFieldKind.Alpha,
                executionField?.Name ?? "Execution field " + (position + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                defaultValue: executionField?.DefaultValue,
                dataType: IddDataType.Alpha);
        }

        return new IddObjectDefinition(name, "EnergyPlus 24.2 execution", fields);
    }

    private static ExecutionField Field(int position, string name, string? defaultValue = null) =>
        new(position, name, defaultValue);

    private static IdfDocument DetachPositioningSchema(IdfDocument source)
    {
        var objects = source.Select(item => new IdfObject(
            item.ObjectType,
            item.Fields.Select(field => new IdfField(
                field.Value,
                field.LeadingComments,
                field.InlineComment)),
            null,
            item.LeadingComments,
            item.HeaderComment));

        return new IdfDocument(
            null,
            objects,
            source.PreambleComments,
            source.TrailingComments);
    }

    private sealed class ExecutionField
    {
        internal ExecutionField(int position, string name, string? defaultValue)
        {
            Position = position;
            Name = name;
            DefaultValue = defaultValue;
        }

        internal int Position { get; }

        internal string Name { get; }

        internal string? DefaultValue { get; }
    }
}

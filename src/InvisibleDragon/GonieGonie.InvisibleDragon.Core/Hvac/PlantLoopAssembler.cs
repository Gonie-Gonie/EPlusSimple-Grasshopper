using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Hvac;

internal static class PlantLoopAssembler
{
    internal static IReadOnlyList<IdfObject> CreateHeatingLoop(
        IdfGenerationContext context,
        SourceSystem source,
        IdfObject sourceObject,
        double pumpMotorEfficiency,
        double setpointTemperature,
        IReadOnlyList<PlantDemandConnection> demandConnections)
    {
        string loop = source.LoopName;
        string pump = $"VSDPump_for_{source.IdfObjectName}";
        string pumpInlet = $"{pump} Water InletNode";
        string pumpOutlet = $"{pump} Water OutletNode";
        string sourceInlet = $"{source.IdfObjectName} Water InletNode";
        string sourceOutlet = $"{source.IdfObjectName} Water OutletNode";
        List<IdfObject> objects = new() { sourceObject };

        objects.Add(context.Create(
            "Pump:VariableSpeed",
            IdfGenerationContext.Field(0, "Name", pump),
            IdfGenerationContext.Field(1, "Inlet Node Name", pumpInlet),
            IdfGenerationContext.Field(2, "Outlet Node Name", pumpOutlet),
            IdfGenerationContext.Field(3, "Design Maximum Flow Rate", "autosize"),
            IdfGenerationContext.Field(5, "Design Power Consumption", "autosize"),
            IdfGenerationContext.Field(6, "Motor Efficiency", pumpMotorEfficiency)));

        string[] pipeRoles = { "Supply Bypass", "Supply Outlet", "Demand Inlet", "Demand Bypass", "Demand Outlet" };
        foreach (string role in pipeRoles)
        {
            objects.Add(Pipe(context, $"{loop} {role} Pipe"));
        }

        var supply = new[]
        {
            Branch(context, $"{loop} Supply Inlet", "Pump:VariableSpeed", pump, pumpInlet, pumpOutlet),
            Branch(context, $"{loop} Supply Bypass", "Pipe:Adiabatic", $"{loop} Supply Bypass Pipe", $"{loop} Supply Bypass Pipe InletNode", $"{loop} Supply Bypass Pipe OutletNode"),
            Branch(context, $"{loop} Supply MainComponent", source.IdfObjectType, source.IdfObjectName, sourceInlet, sourceOutlet),
            Branch(context, $"{loop} Supply Outlet", "Pipe:Adiabatic", $"{loop} Supply Outlet Pipe", $"{loop} Supply Outlet Pipe InletNode", $"{loop} Supply Outlet Pipe OutletNode"),
        };
        objects.AddRange(supply);

        IdfObject demandInlet = Branch(context, $"{loop} Demand Inlet", "Pipe:Adiabatic", $"{loop} Demand Inlet Pipe", $"{loop} Demand Inlet Pipe InletNode", $"{loop} Demand Inlet Pipe OutletNode");
        IdfObject demandBypass = Branch(context, $"{loop} Demand Bypass", "Pipe:Adiabatic", $"{loop} Demand Bypass Pipe", $"{loop} Demand Bypass Pipe InletNode", $"{loop} Demand Bypass Pipe OutletNode");
        IdfObject demandOutlet = Branch(context, $"{loop} Demand Outlet", "Pipe:Adiabatic", $"{loop} Demand Outlet Pipe", $"{loop} Demand Outlet Pipe InletNode", $"{loop} Demand Outlet Pipe OutletNode");
        objects.Add(demandInlet);
        objects.Add(demandBypass);
        foreach (PlantDemandConnection demand in demandConnections)
        {
            objects.Add(Branch(context, demand.BranchName, demand.ComponentObjectType, demand.ComponentName, demand.InletNodeName, demand.OutletNodeName));
        }

        objects.Add(demandOutlet);

        string[] supplyBranches = supply.Select(item => item.Name!).ToArray();
        string[] demandBranches = new[] { demandInlet.Name!, demandBypass.Name! }
            .Concat(demandConnections.Select(item => item.BranchName))
            .Concat(new[] { demandOutlet.Name! })
            .ToArray();
        objects.Add(context.CreateRaw("BranchList", new object?[] { $"{loop} Supply BranchList" }.Concat(supplyBranches.Cast<object?>()).ToArray()));
        objects.Add(context.CreateRaw("BranchList", new object?[] { $"{loop} Demand BranchList" }.Concat(demandBranches.Cast<object?>()).ToArray()));

        objects.Add(context.CreateRaw("Connector:Splitter", new object?[] { $"{loop} Supply Splitter", supplyBranches[0], supplyBranches[2], supplyBranches[1] }));
        objects.Add(context.CreateRaw("Connector:Mixer", new object?[] { $"{loop} Supply Mixer", supplyBranches[3], supplyBranches[2], supplyBranches[1] }));
        objects.Add(context.CreateRaw("ConnectorList", $"{loop} Supply Connectors", "Connector:Splitter", $"{loop} Supply Splitter", "Connector:Mixer", $"{loop} Supply Mixer"));
        string[] demandParallel = new[] { demandBranches[1] }.Concat(demandConnections.Select(item => item.BranchName)).ToArray();
        objects.Add(context.CreateRaw("Connector:Splitter", new object?[] { $"{loop} Demand Splitter", demandBranches[0] }.Concat(demandParallel.Cast<object?>()).ToArray()));
        objects.Add(context.CreateRaw("Connector:Mixer", new object?[] { $"{loop} Demand Mixer", demandBranches[demandBranches.Length - 1] }.Concat(demandParallel.Cast<object?>()).ToArray()));
        objects.Add(context.CreateRaw("ConnectorList", $"{loop} Demand Connectors", "Connector:Splitter", $"{loop} Demand Splitter", "Connector:Mixer", $"{loop} Demand Mixer"));

        objects.Add(context.CreateRaw("PlantEquipmentList", $"{loop} EquipmentList", source.IdfObjectType, source.IdfObjectName));
        objects.Add(context.CreateRaw("PlantEquipmentOperation:HeatingLoad", $"{loop} Operation", 0, 1E20, $"{loop} EquipmentList"));
        objects.Add(context.CreateRaw("PlantEquipmentOperationSchemes", $"{loop} OperationScheme", "PlantEquipmentOperation:HeatingLoad", $"{loop} Operation", "ALLON"));
        objects.Add(context.CreateRaw("Schedule:Constant", $"{loop} SetpointTemperature", "ScheduleTypeLimits:Temperature", setpointTemperature));
        objects.Add(context.CreateRaw("SetpointManager:Scheduled", $"{loop} SetpointManager", "Temperature", $"{loop} SetpointTemperature", $"{loop} Supply Outlet Pipe OutletNode"));
        objects.Add(context.CreateRaw("AvailabilityManager:Scheduled", $"{loop} AvailabilityManager", "ALLON"));
        objects.Add(context.CreateRaw("AvailabilityManagerAssignmentList", $"{loop} AvailabilityManagerAssignmentList", "AvailabilityManager:Scheduled", $"{loop} AvailabilityManager"));
        objects.Add(context.Create(
            "PlantLoop",
            IdfGenerationContext.Field(0, "Name", loop),
            IdfGenerationContext.Field(1, "Fluid Type", "Water"),
            IdfGenerationContext.Field(3, "Plant Equipment Operation Scheme Name", $"{loop} OperationScheme"),
            IdfGenerationContext.Field(4, "Loop Temperature Setpoint Node Name", $"{loop} Supply Outlet Pipe OutletNode"),
            IdfGenerationContext.Field(5, "Maximum Loop Temperature", 80),
            IdfGenerationContext.Field(6, "Minimum Loop Temperature", 0.1),
            IdfGenerationContext.Field(7, "Maximum Loop Flow Rate", "autosize"),
            IdfGenerationContext.Field(10, "Plant Side Inlet Node Name", pumpInlet),
            IdfGenerationContext.Field(11, "Plant Side Outlet Node Name", $"{loop} Supply Outlet Pipe OutletNode"),
            IdfGenerationContext.Field(12, "Plant Side Branch List Name", $"{loop} Supply BranchList"),
            IdfGenerationContext.Field(13, "Plant Side Connector List Name", $"{loop} Supply Connectors"),
            IdfGenerationContext.Field(14, "Demand Side Inlet Node Name", $"{loop} Demand Inlet Pipe InletNode"),
            IdfGenerationContext.Field(15, "Demand Side Outlet Node Name", $"{loop} Demand Outlet Pipe OutletNode"),
            IdfGenerationContext.Field(16, "Demand Side Branch List Name", $"{loop} Demand BranchList"),
            IdfGenerationContext.Field(17, "Demand Side Connector List Name", $"{loop} Demand Connectors"),
            IdfGenerationContext.Field(18, "Availability Manager List Name", $"{loop} AvailabilityManagerAssignmentList")));
        objects.Add(context.CreateRaw("Sizing:Plant", loop, "Heating", setpointTemperature, 11));
        return objects;
    }

    private static IdfObject Pipe(IdfGenerationContext context, string name) => context.CreateRaw(
        "Pipe:Adiabatic",
        name,
        $"{name} InletNode",
        $"{name} OutletNode");

    private static IdfObject Branch(
        IdfGenerationContext context,
        string name,
        string objectType,
        string objectName,
        string inlet,
        string outlet) => context.CreateRaw("Branch", name, string.Empty, objectType, objectName, inlet, outlet);
}

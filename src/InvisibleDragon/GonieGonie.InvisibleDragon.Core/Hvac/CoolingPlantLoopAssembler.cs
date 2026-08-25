using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Hvac;

/// <summary>
/// Builds the chilled-water and condenser-water topologies used by cold sources.
/// </summary>
internal static class CoolingPlantLoopAssembler
{
    internal static IReadOnlyList<IdfObject> CreateCoolingLoop(
        IdfGenerationContext context,
        SourceSystem source,
        IdfObject sourceObject,
        string sourceInletNodeName,
        string sourceOutletNodeName,
        double pumpMotorEfficiency,
        double setpointTemperatureCelsius,
        IReadOnlyList<PlantDemandConnection> demandConnections,
        string availabilityScheduleName = "ALLON")
    {
        string loop = source.LoopName;
        string sourceObjectType = SourceObjectType(context, source);
        string pump = $"VSDPump_for_{source.IdfObjectName}";
        string pumpInlet = $"{pump} Water InletNode";
        string pumpOutlet = $"{pump} Water OutletNode";
        var objects = new List<IdfObject> { sourceObject };

        objects.Add(VariableSpeedPump(context, pump, pumpInlet, pumpOutlet, pumpMotorEfficiency));
        AddPipes(objects, context, loop);

        IdfObject[] supply =
        {
            Branch(context, $"{loop} Supply Inlet", "Pump:VariableSpeed", pump, pumpInlet, pumpOutlet),
            Branch(
                context,
                $"{loop} Supply Bypass",
                "Pipe:Adiabatic",
                $"{loop} Supply Bypass Pipe",
                $"{loop} Supply Bypass Pipe InletNode",
                $"{loop} Supply Bypass Pipe OutletNode"),
            Branch(
                context,
                $"{loop} Supply MainComponent",
                sourceObjectType,
                source.IdfObjectName,
                sourceInletNodeName,
                sourceOutletNodeName),
            Branch(
                context,
                $"{loop} Supply Outlet",
                "Pipe:Adiabatic",
                $"{loop} Supply Outlet Pipe",
                $"{loop} Supply Outlet Pipe InletNode",
                $"{loop} Supply Outlet Pipe OutletNode"),
        };
        objects.AddRange(supply);

        IdfObject demandInlet = Branch(
            context,
            $"{loop} Demand Inlet",
            "Pipe:Adiabatic",
            $"{loop} Demand Inlet Pipe",
            $"{loop} Demand Inlet Pipe InletNode",
            $"{loop} Demand Inlet Pipe OutletNode");
        IdfObject demandBypass = Branch(
            context,
            $"{loop} Demand Bypass",
            "Pipe:Adiabatic",
            $"{loop} Demand Bypass Pipe",
            $"{loop} Demand Bypass Pipe InletNode",
            $"{loop} Demand Bypass Pipe OutletNode");
        IdfObject demandOutlet = Branch(
            context,
            $"{loop} Demand Outlet",
            "Pipe:Adiabatic",
            $"{loop} Demand Outlet Pipe",
            $"{loop} Demand Outlet Pipe InletNode",
            $"{loop} Demand Outlet Pipe OutletNode");
        objects.Add(demandInlet);
        objects.Add(demandBypass);
        foreach (PlantDemandConnection demand in demandConnections)
        {
            objects.Add(Branch(
                context,
                demand.BranchName,
                demand.ComponentObjectType,
                demand.ComponentName,
                demand.InletNodeName,
                demand.OutletNodeName));
        }

        objects.Add(demandOutlet);

        string[] supplyBranches = supply.Select(item => item.Name!).ToArray();
        string[] demandBranches = new[] { demandInlet.Name!, demandBypass.Name! }
            .Concat(demandConnections.Select(item => item.BranchName))
            .Concat(new[] { demandOutlet.Name! })
            .ToArray();
        AddBranchAndConnectorLists(objects, context, loop, supplyBranches, demandBranches);

        objects.Add(context.CreateRaw(
            "PlantEquipmentList",
            $"{loop} EquipmentList",
            sourceObjectType,
            source.IdfObjectName));
        objects.Add(context.CreateRaw(
            "PlantEquipmentOperation:CoolingLoad",
            $"{loop} Operation",
            0,
            1E20,
            $"{loop} EquipmentList"));
        objects.Add(context.CreateRaw(
            "PlantEquipmentOperationSchemes",
            $"{loop} OperationScheme",
            "PlantEquipmentOperation:CoolingLoad",
            $"{loop} Operation",
            "ALLON"));
        objects.Add(context.CreateRaw(
            "Schedule:Constant",
            $"{loop} SetpointTemperature",
            context.Options.UseLegacySimpleDragonScheduleMetadata
                ? null
                : "ScheduleTypeLimits:Temperature",
            setpointTemperatureCelsius));
        objects.Add(context.CreateRaw(
            "SetpointManager:Scheduled",
            $"{loop} SetpointManager",
            "Temperature",
            $"{loop} SetpointTemperature",
            $"{loop} Supply Outlet Pipe OutletNode"));
        objects.Add(context.CreateRaw(
            "AvailabilityManager:Scheduled",
            $"{loop} AvailabilityManager",
            availabilityScheduleName));
        objects.Add(context.CreateRaw(
            "AvailabilityManagerAssignmentList",
            $"{loop} AvailabilityManagerAssignmentList",
            "AvailabilityManager:Scheduled",
            $"{loop} AvailabilityManager"));
        objects.Add(context.Create(
            "PlantLoop",
            IdfGenerationContext.Field(0, "Name", loop),
            IdfGenerationContext.Field(1, "Fluid Type", "Water"),
            IdfGenerationContext.Field(3, "Plant Equipment Operation Scheme Name", $"{loop} OperationScheme"),
            IdfGenerationContext.Field(4, "Loop Temperature Setpoint Node Name", $"{loop} Supply Outlet Pipe OutletNode"),
            IdfGenerationContext.Field(5, "Maximum Loop Temperature", 80),
            IdfGenerationContext.Field(6, "Minimum Loop Temperature", 0.1),
            IdfGenerationContext.Field(7, "Maximum Loop Flow Rate", "autosize"),
            IdfGenerationContext.Field(8, "Minimum Loop Flow Rate", 0),
            IdfGenerationContext.Field(9, "Plant Loop Volume", "autocalculate"),
            IdfGenerationContext.Field(10, "Plant Side Inlet Node Name", pumpInlet),
            IdfGenerationContext.Field(11, "Plant Side Outlet Node Name", $"{loop} Supply Outlet Pipe OutletNode"),
            IdfGenerationContext.Field(12, "Plant Side Branch List Name", $"{loop} Supply BranchList"),
            IdfGenerationContext.Field(13, "Plant Side Connector List Name", $"{loop} Supply Connectors"),
            IdfGenerationContext.Field(14, "Demand Side Inlet Node Name", $"{loop} Demand Inlet Pipe InletNode"),
            IdfGenerationContext.Field(15, "Demand Side Outlet Node Name", $"{loop} Demand Outlet Pipe OutletNode"),
            IdfGenerationContext.Field(16, "Demand Side Branch List Name", $"{loop} Demand BranchList"),
            IdfGenerationContext.Field(17, "Demand Side Connector List Name", $"{loop} Demand Connectors"),
            IdfGenerationContext.Field(19, "Availability Manager List Name", $"{loop} AvailabilityManagerAssignmentList")));
        objects.Add(context.CreateRaw(
            "Sizing:Plant",
            loop,
            "Cooling",
            setpointTemperatureCelsius,
            4));
        return objects;
    }

    internal static IReadOnlyList<IdfObject> CreateCondenserLoop(
        IdfGenerationContext context,
        CoolingTower tower,
        SourceSystem source,
        IdfObject towerObject,
        string condenserInletNodeName,
        string condenserOutletNodeName)
    {
        string loop = CoolingTower.LoopNameFor(source);
        string sourceObjectType = SourceObjectType(context, source);
        string towerName = CoolingTower.ObjectNameFor(source);
        string pump = $"VSDPump_for_{towerName}";
        string pumpInlet = $"{pump} Water InletNode";
        string pumpOutlet = $"{pump} Water OutletNode";
        var objects = new List<IdfObject> { towerObject };

        objects.Add(VariableSpeedPump(
            context,
            pump,
            pumpInlet,
            pumpOutlet,
            tower.PumpMotorEfficiency));
        AddPipes(objects, context, loop);

        IdfObject[] supply =
        {
            Branch(context, $"{loop} Supply Inlet", "Pump:VariableSpeed", pump, pumpInlet, pumpOutlet),
            Branch(
                context,
                $"{loop} Supply Bypass",
                "Pipe:Adiabatic",
                $"{loop} Supply Bypass Pipe",
                $"{loop} Supply Bypass Pipe InletNode",
                $"{loop} Supply Bypass Pipe OutletNode"),
            Branch(
                context,
                $"{loop} Supply MainComponent",
                tower.IdfObjectType,
                towerName,
                $"{towerName} Water InletNode",
                $"{towerName} Water OutletNode"),
            Branch(
                context,
                $"{loop} Supply Outlet",
                "Pipe:Adiabatic",
                $"{loop} Supply Outlet Pipe",
                $"{loop} Supply Outlet Pipe InletNode",
                $"{loop} Supply Outlet Pipe OutletNode"),
        };
        objects.AddRange(supply);

        IdfObject[] demand =
        {
            Branch(
                context,
                $"{loop} Demand Inlet",
                "Pipe:Adiabatic",
                $"{loop} Demand Inlet Pipe",
                $"{loop} Demand Inlet Pipe InletNode",
                $"{loop} Demand Inlet Pipe OutletNode"),
            Branch(
                context,
                $"{loop} Demand Bypass",
                "Pipe:Adiabatic",
                $"{loop} Demand Bypass Pipe",
                $"{loop} Demand Bypass Pipe InletNode",
                $"{loop} Demand Bypass Pipe OutletNode"),
            Branch(
                context,
                $"{loop} Demand MainChiller",
                sourceObjectType,
                source.IdfObjectName,
                condenserInletNodeName,
                condenserOutletNodeName),
            Branch(
                context,
                $"{loop} Demand Outlet",
                "Pipe:Adiabatic",
                $"{loop} Demand Outlet Pipe",
                $"{loop} Demand Outlet Pipe InletNode",
                $"{loop} Demand Outlet Pipe OutletNode"),
        };
        objects.AddRange(demand);

        string[] supplyBranches = supply.Select(item => item.Name!).ToArray();
        string[] demandBranches = demand.Select(item => item.Name!).ToArray();
        AddBranchAndConnectorLists(objects, context, loop, supplyBranches, demandBranches);

        objects.Add(context.CreateRaw(
            "CondenserEquipmentList",
            $"{loop} EquipmentList",
            tower.IdfObjectType,
            towerName));
        objects.Add(context.CreateRaw(
            "PlantEquipmentOperation:CoolingLoad",
            $"{loop} Operation",
            0,
            1E20,
            $"{loop} EquipmentList"));
        objects.Add(context.CreateRaw(
            "CondenserEquipmentOperationSchemes",
            $"{loop} OperationScheme",
            "PlantEquipmentOperation:CoolingLoad",
            $"{loop} Operation",
            "ALLON"));
        objects.Add(context.Create(
            "SetpointManager:FollowOutdoorAirTemperature",
            IdfGenerationContext.Field(0, "Name", $"{loop} SetpointManager"),
            IdfGenerationContext.Field(1, "Control Variable", "Temperature"),
            IdfGenerationContext.Field(2, "Reference Temperature Type", "OutdoorAirWetBulb"),
            IdfGenerationContext.Field(3, "Offset Temperature Difference", 1.5),
            IdfGenerationContext.Field(4, "Maximum Setpoint Temperature", 50),
            IdfGenerationContext.Field(5, "Minimum Setpoint Temperature", 20),
            IdfGenerationContext.Field(6, "Setpoint Node or NodeList Name", $"{loop} Supply Outlet Pipe OutletNode")));
        objects.Add(context.Create(
            "CondenserLoop",
            IdfGenerationContext.Field(0, "Name", loop),
            IdfGenerationContext.Field(1, "Fluid Type", "Water"),
            IdfGenerationContext.Field(3, "Condenser Equipment Operation Scheme Name", $"{loop} OperationScheme"),
            IdfGenerationContext.Field(4, "Condenser Loop Temperature Setpoint Node Name", $"{loop} Supply Outlet Pipe OutletNode"),
            IdfGenerationContext.Field(5, "Maximum Loop Temperature", 50),
            IdfGenerationContext.Field(6, "Minimum Loop Temperature", 5),
            IdfGenerationContext.Field(7, "Maximum Loop Flow Rate", "autosize"),
            IdfGenerationContext.Field(8, "Minimum Loop Flow Rate", 0),
            IdfGenerationContext.Field(9, "Condenser Loop Volume", "autocalculate"),
            IdfGenerationContext.Field(10, "Condenser Side Inlet Node Name", pumpInlet),
            IdfGenerationContext.Field(11, "Condenser Side Outlet Node Name", $"{loop} Supply Outlet Pipe OutletNode"),
            IdfGenerationContext.Field(12, "Condenser Side Branch List Name", $"{loop} Supply BranchList"),
            IdfGenerationContext.Field(13, "Condenser Side Connector List Name", $"{loop} Supply Connectors"),
            IdfGenerationContext.Field(14, "Demand Side Inlet Node Name", $"{loop} Demand Inlet Pipe InletNode"),
            IdfGenerationContext.Field(15, "Demand Side Outlet Node Name", $"{loop} Demand Outlet Pipe OutletNode"),
            IdfGenerationContext.Field(16, "Condenser Demand Side Branch List Name", $"{loop} Demand BranchList"),
            IdfGenerationContext.Field(17, "Condenser Demand Side Connector List Name", $"{loop} Demand Connectors")));
        objects.Add(context.CreateRaw("Sizing:Plant", loop, "Condenser", 29, 5));
        return objects;
    }

    private static void AddPipes(
        List<IdfObject> objects,
        IdfGenerationContext context,
        string loop)
    {
        string[] roles =
        {
            "Supply Bypass",
            "Supply Outlet",
            "Demand Inlet",
            "Demand Bypass",
            "Demand Outlet",
        };
        foreach (string role in roles)
        {
            objects.Add(Pipe(context, $"{loop} {role} Pipe"));
        }
    }

    private static void AddBranchAndConnectorLists(
        List<IdfObject> objects,
        IdfGenerationContext context,
        string loop,
        IReadOnlyList<string> supplyBranches,
        IReadOnlyList<string> demandBranches)
    {
        objects.Add(context.CreateRaw(
            "BranchList",
            new object?[] { $"{loop} Supply BranchList" }
                .Concat(supplyBranches.Cast<object?>())
                .ToArray()));
        objects.Add(context.CreateRaw(
            "BranchList",
            new object?[] { $"{loop} Demand BranchList" }
                .Concat(demandBranches.Cast<object?>())
                .ToArray()));
        objects.Add(context.CreateRaw(
            "Connector:Splitter",
            new object?[] { $"{loop} Supply Splitter", supplyBranches[0] }
                .Concat(new object?[] { supplyBranches[2], supplyBranches[1] })
                .ToArray()));
        objects.Add(context.CreateRaw(
            "Connector:Mixer",
            new object?[] { $"{loop} Supply Mixer", supplyBranches[3] }
                .Concat(new object?[] { supplyBranches[2], supplyBranches[1] })
                .ToArray()));
        objects.Add(context.CreateRaw(
            "ConnectorList",
            $"{loop} Supply Connectors",
            "Connector:Splitter",
            $"{loop} Supply Splitter",
            "Connector:Mixer",
            $"{loop} Supply Mixer"));

        string[] demandParallel = demandBranches.Skip(1).Take(demandBranches.Count - 2).ToArray();
        objects.Add(context.CreateRaw(
            "Connector:Splitter",
            new object?[] { $"{loop} Demand Splitter", demandBranches[0] }
                .Concat(demandParallel.Cast<object?>())
                .ToArray()));
        objects.Add(context.CreateRaw(
            "Connector:Mixer",
            new object?[] { $"{loop} Demand Mixer", demandBranches[demandBranches.Count - 1] }
                .Concat(demandParallel.Cast<object?>())
                .ToArray()));
        objects.Add(context.CreateRaw(
            "ConnectorList",
            $"{loop} Demand Connectors",
            "Connector:Splitter",
            $"{loop} Demand Splitter",
            "Connector:Mixer",
            $"{loop} Demand Mixer"));
    }

    private static IdfObject VariableSpeedPump(
        IdfGenerationContext context,
        string name,
        string inlet,
        string outlet,
        double motorEfficiency) => context.Create(
            "Pump:VariableSpeed",
            IdfGenerationContext.Field(0, "Name", name),
            IdfGenerationContext.Field(1, "Inlet Node Name", inlet),
            IdfGenerationContext.Field(2, "Outlet Node Name", outlet),
            IdfGenerationContext.Field(3, "Design Maximum Flow Rate", "autosize"),
            IdfGenerationContext.Field(4, "Design Pump Head", 179352),
            IdfGenerationContext.Field(5, "Design Power Consumption", "autosize"),
            IdfGenerationContext.Field(6, "Motor Efficiency", motorEfficiency),
            IdfGenerationContext.Field(7, "Fraction of Motor Inefficiencies to Fluid Stream", 0),
            IdfGenerationContext.Field(8, "Coefficient 1 of the Part Load Performance Curve", 0),
            IdfGenerationContext.Field(9, "Coefficient 2 of the Part Load Performance Curve", 1),
            IdfGenerationContext.Field(10, "Coefficient 3 of the Part Load Performance Curve", 0),
            IdfGenerationContext.Field(11, "Coefficient 4 of the Part Load Performance Curve", 0),
            IdfGenerationContext.Field(
                12,
                "Design Minimum Flow Rate",
                context.Options.UseLegacySimpleDragonHvacTopology ? "autosize" : (object)0),
            IdfGenerationContext.Field(
                13,
                "Pump Control Type",
                context.Options.UseLegacySimpleDragonHvacTopology ? "Continuous" : "Intermittent"),
            IdfGenerationContext.Field(25, "Design Power Sizing Method", "PowerPerFlowPerPressure"),
            IdfGenerationContext.Field(26, "Design Electric Power per Unit Flow Rate", 348701.1),
            IdfGenerationContext.Field(27, "Design Shaft Power per Unit Flow Rate per Unit Head", 1.282051282),
            IdfGenerationContext.Field(28, "Design Minimum Flow Rate Fraction", 0),
            IdfGenerationContext.Field(29, "End-Use Subcategory", "General"));

    private static string SourceObjectType(IdfGenerationContext context, SourceSystem source)
    {
        return source is Chiller chiller
            ? chiller.IdfObjectTypeFor(context)
            : source.IdfObjectType;
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
        string outlet) => context.CreateRaw(
            "Branch",
            name,
            string.Empty,
            objectType,
            objectName,
            inlet,
            outlet);
}

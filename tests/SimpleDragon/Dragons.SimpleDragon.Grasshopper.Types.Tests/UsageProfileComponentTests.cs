using System.Collections;
using System.Reflection;
using Dragons.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;

namespace Dragons.SimpleDragon.Grasshopper.Tests;

public sealed class UsageProfileComponentTests
{
    private static readonly string[] ExpectedOutputNames = { "Profile", "Diagnostics" };

    [Fact]
    public void ProfileUsesPackagedNativeChoiceInModelPanel()
    {
        GH_Component component = Component();

        Assert.Equal("Model", component.SubCategory);
        IGH_Param input = Assert.Single(component.Params.Input);
        Assert.Equal("Name", input.Name);
        Assert.Equal("N", input.NickName);
        Assert.Equal(GH_ParamAccess.item, input.Access);
        Assert.Equal("ChoiceStringParam", input.GetType().Name);

        Param_String choice = Assert.IsAssignableFrom<Param_String>(input);
        Assert.Equal(
            SimpleDragonDatabase.Default.UsageProfiles.Items[0].Name,
            Assert.IsType<GH_String>(Assert.Single(choice.PersistentData.AllData(true))).Value);

        FieldInfo allowedValuesField = Assert.IsAssignableFrom<FieldInfo>(input.GetType().GetField(
            "_allowedValues",
            BindingFlags.Instance | BindingFlags.NonPublic));
        string[] allowedValues = Assert.IsType<string[]>(allowedValuesField.GetValue(input));
        Assert.Equal(
            SimpleDragonDatabase.Default.UsageProfiles.Items.Select(profile => profile.Name),
            allowedValues);
        Assert.Equal(24, allowedValues.Length);

        Assert.Equal(
            ExpectedOutputNames,
            component.Params.Output.Select(parameter => parameter.Name));
        Assert.Equal("SimpleDragonUsageProfileParam", component.Params.Output[0].GetType().Name);
        Assert.Equal("SimpleDragonDiagnosticParam", component.Params.Output[1].GetType().Name);
    }

    [Fact]
    public void SelectedPackagedProfileResolvesWithoutCopyPasteLoop()
    {
        GH_Component component = Component();
        IGH_DataAccess access = DispatchProxy.Create<IGH_DataAccess, ProfileDataAccess>();
        var probe = (ProfileDataAccess)(object)access;
        string selectedName = SimpleDragonDatabase.Default.UsageProfiles.Items[7].Name;
        probe.Inputs[0] = selectedName;

        InvokeSolve(component, access);

        SimpleDragonUsageProfileGoo profile = Assert.IsType<SimpleDragonUsageProfileGoo>(probe.Outputs[0]);
        Assert.Equal(selectedName, profile.Value.Name);
        Assert.Empty(probe.OutputLists[1]);
    }

    private static GH_Component Component()
    {
        Assembly assembly = Assembly.LoadFrom(Path.Combine(
            RepositoryRoot(),
            "temp",
            "build",
            "bin",
            "Dragons.SimpleDragon.GH",
            "Release",
            "net8.0-windows",
            "Dragons.SimpleDragon.GH.gha"));
        Type type = Assert.Single(
            assembly.GetTypes(),
            candidate => string.Equals(
                candidate.Name,
                "LookupUsageProfileComponent",
                StringComparison.Ordinal));
        return Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
    }

    private static void InvokeSolve(GH_Component component, IGH_DataAccess access)
    {
        Type? current = component.GetType();
        MethodInfo? solve = null;
        while (current is not null && solve is null)
        {
            solve = current.GetMethod(
                "SolveInstance",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            current = current.BaseType;
        }

        Assert.NotNull(solve);
        solve.Invoke(component, new object[] { access });
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null
            && !File.Exists(Path.Combine(current.FullName, "Dragons.Grasshopper.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy creates a runtime subclass of this probe.")]
    private class ProfileDataAccess : DispatchProxy
    {
        internal Dictionary<int, object?> Inputs { get; } = new();

        internal Dictionary<int, object?> Outputs { get; } = new();

        internal Dictionary<int, IReadOnlyList<object?>> OutputLists { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.NotNull(targetMethod);
            if (string.Equals(targetMethod!.Name, "GetData", StringComparison.Ordinal)
                && args is { Length: 2 }
                && args[0] is int inputIndex
                && Inputs.TryGetValue(inputIndex, out object? input))
            {
                args[1] = input;
                return true;
            }

            if (string.Equals(targetMethod.Name, "SetData", StringComparison.Ordinal)
                && args is { Length: >= 2 }
                && args[0] is int outputIndex)
            {
                Outputs[outputIndex] = args[1];
                return true;
            }

            if (string.Equals(targetMethod.Name, "SetDataList", StringComparison.Ordinal)
                && args is { Length: >= 2 }
                && args[0] is int listIndex
                && args[1] is IEnumerable values)
            {
                OutputLists[listIndex] = values.Cast<object?>().ToArray();
                return true;
            }

            if (targetMethod.ReturnType == typeof(bool))
            {
                return false;
            }

            if (targetMethod.ReturnType == typeof(int))
            {
                return 0;
            }

            return null;
        }
    }
}

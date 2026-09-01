using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace Dragons.SimpleDragon.Grasshopper.Tests;

[SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "The inline pair keeps the small component port contract readable.")]
public sealed class SimpleDragonResultComponentContractTests
{
    [Fact]
    public void GreenRetrofitSummaryMetricUsesNamedSelectableValues()
    {
        Assembly assembly = LoadPlugin();
        Type type = Assert.Single(
            assembly.GetTypes(),
            item => item.Name == "GreenRetrofitResultSummaryComponent");
        GH_Component component = Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));

        Assert.Equal(new[] { "GRR", "Metric", "Gross" }, component.Params.Input.Select(item => item.Name));
        Assert.Equal("ChoiceStringParam", component.Params.Input[1].GetType().Name);
        Assert.Equal(nameof(GreenRetrofitMetric.SiteUses), PersistentDefault(component.Params.Input[1]));
        Assert.Contains("Site Uses", component.Params.Input[1].Description, StringComparison.Ordinal);
        Assert.Contains("Source Uses", component.Params.Input[1].Description, StringComparison.Ordinal);
        Assert.Contains("Carbon", component.Params.Input[1].Description, StringComparison.Ordinal);
        Assert.Contains("Cost", component.Params.Input[1].Description, StringComparison.Ordinal);
    }

    private static object? PersistentDefault(IGH_Param parameter)
    {
        PropertyInfo? persistentData = parameter.GetType().GetProperty("PersistentData");
        object? structure = persistentData?.GetValue(parameter);
        MethodInfo? allData = structure?.GetType().GetMethod(
            "AllData",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(bool) },
            modifiers: null);
        IEnumerable? data = allData?.Invoke(structure, new object[] { true }) as IEnumerable;
        object? first = data?.Cast<object>().FirstOrDefault();
        return first is IGH_Goo goo ? goo.ScriptVariable() : first;
    }

    private static Assembly LoadPlugin()
    {
        string path = Path.Combine(
            RepositoryRoot(),
            "temp",
            "build",
            "bin",
            "Dragons.SimpleDragon.GH",
            "Release",
            "net8.0-windows",
            "Dragons.SimpleDragon.GH.gha");
        Assert.True(File.Exists(path), "Expected built Grasshopper assembly at '" + path + "'.");
        return Assembly.LoadFrom(path);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dragons.Grasshopper.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace GonieGonie.Dragons.GrasshopperSmoke;

[DataContract]
internal sealed class GrasshopperSmokeSummary
{
    [DataMember(Name = "schema", Order = 1)]
    public string Schema { get; set; } = "goniegonie.dragons-grasshopper.host-smoke.v3";

    [DataMember(Name = "host", Order = 2)]
    public string Host { get; set; } = string.Empty;

    [DataMember(Name = "rhinoVersion", Order = 3)]
    public string RhinoVersion { get; set; } = string.Empty;

    [DataMember(Name = "grasshopperVersion", Order = 4)]
    public string GrasshopperVersion { get; set; } = string.Empty;

    [DataMember(Name = "scenario", Order = 5)]
    public string Scenario { get; set; } = string.Empty;

    [DataMember(Name = "source", Order = 6)]
    public string Source { get; set; } = string.Empty;

    [DataMember(Name = "pluginCount", Order = 7)]
    public int PluginCount { get; set; }

    [DataMember(Name = "pluginPaths", Order = 8)]
    public string[] PluginPaths { get; set; } = Array.Empty<string>();

    [DataMember(Name = "pluginArtifacts", Order = 9)]
    public ArtifactProvenanceSummary[] PluginArtifacts { get; set; } =
        Array.Empty<ArtifactProvenanceSummary>();

    [DataMember(Name = "portableArchives", Order = 10)]
    public ArtifactProvenanceSummary[] PortableArchives { get; set; } =
        Array.Empty<ArtifactProvenanceSummary>();

    [DataMember(Name = "registeredInvisibleComponents", Order = 11)]
    public int RegisteredInvisibleComponents { get; set; }

    [DataMember(Name = "registeredInvisibleParameters", Order = 12)]
    public int RegisteredInvisibleParameters { get; set; }

    [DataMember(Name = "registeredSimpleComponents", Order = 13)]
    public int RegisteredSimpleComponents { get; set; }

    [DataMember(Name = "registeredSimpleParameters", Order = 14)]
    public int RegisteredSimpleParameters { get; set; }

    [DataMember(Name = "reopenedObjectCount", Order = 15)]
    public int ReopenedObjectCount { get; set; }

    [DataMember(Name = "persistence", Order = 16)]
    public PersistenceSummary[] Persistence { get; set; } = Array.Empty<PersistenceSummary>();

    [DataMember(Name = "documentPath", Order = 17)]
    public string DocumentPath { get; set; } = string.Empty;

    internal void Write(string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        var serializer = new DataContractJsonSerializer(typeof(GrasshopperSmokeSummary));
        serializer.WriteObject(stream, this);
    }

    internal void WriteLegacyText(string path)
    {
        var lines = new[]
        {
            "Rhino=" + RhinoVersion,
            "Scenario=" + Scenario,
            "Source=" + Source,
            "PluginCount=" + PluginCount,
            "PluginPaths=" + string.Join("|", PluginPaths),
            "PluginSha256=" + string.Join("|", PluginArtifacts.Select(item => item.Sha256)),
            "PortableArchives=" + string.Join("|", PortableArchives.Select(item => item.Path)),
            "PortableArchiveSha256=" + string.Join("|", PortableArchives.Select(item => item.Sha256)),
            "InvisibleComponents=" + RegisteredInvisibleComponents,
            "InvisibleParameters=" + RegisteredInvisibleParameters,
            "SimpleComponents=" + RegisteredSimpleComponents,
            "SimpleParameters=" + RegisteredSimpleParameters,
            "ReopenedObjects=" + ReopenedObjectCount
        };
        File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    internal string ToConsoleText()
    {
        return $"Grasshopper host gate passed: scenario={Scenario}, source={Source}, "
            + $"plugins={PluginCount}, InvisibleDragon={RegisteredInvisibleComponents}+{RegisteredInvisibleParameters}, "
            + $"SimpleDragon={RegisteredSimpleComponents}+{RegisteredSimpleParameters}, "
            + $"reopened={ReopenedObjectCount}.";
    }
}

[DataContract]
internal sealed class ArtifactProvenanceSummary
{
    [DataMember(Name = "product", Order = 1)]
    public string Product { get; set; } = string.Empty;

    [DataMember(Name = "path", Order = 2)]
    public string Path { get; set; } = string.Empty;

    [DataMember(Name = "sha256", Order = 3)]
    public string Sha256 { get; set; } = string.Empty;

    internal static ArtifactProvenanceSummary From(SmokeArtifactProvenance source)
    {
        return new ArtifactProvenanceSummary
        {
            Product = source.Product,
            Path = source.Path,
            Sha256 = source.Sha256
        };
    }
}

[DataContract]
internal sealed class PersistenceSummary
{
    [DataMember(Name = "product", Order = 1)]
    public string Product { get; set; } = string.Empty;

    [DataMember(Name = "gooType", Order = 2)]
    public string GooType { get; set; } = string.Empty;

    [DataMember(Name = "valueProperty", Order = 3)]
    public string ValueProperty { get; set; } = string.Empty;

    [DataMember(Name = "value", Order = 4)]
    public string Value { get; set; } = string.Empty;
}

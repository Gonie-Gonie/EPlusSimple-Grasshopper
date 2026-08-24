using System.IO.Compression;
using System.Text;
using GonieGonie.InvisibleDragon.Idd;

namespace GonieGonie.InvisibleDragon.Tests.Idd;

public sealed class IddParserTests
{
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void ParsesObjectAndFieldDirectivesWithoutLosingInclusiveSemantics()
    {
        const string text = """
            !IDD_Version 24.2.0
            !IDD_BUILD abc123
            \group Test Group
            Test:Object,
              \memo Object memo
              \unique-object
              \required-object
              \min-fields 2
              \extensible:2
              \format vertices
              A1, \field Name
                  \required-field
                  \type alpha
                  \retaincase
                  \reference TestNames
              N1, \field Size
                  \note first note
                  \units m
                  \ip-units ft
                  \minimum> 0
                  \maximum 10
                  \default 1.5
                  \autosizable
              A2, \field Target
                  \type object-list
                  \object-list TestNames
                  \begin-extensible
              A3; \field Mode
                  \type choice
                  \key On
                  \key Off
                  \default On
                  \autocalculatable
                  \deprecated
                  \reference-class-name TestClasses
            """;

        IddSchema schema = IddParser.Parse(text, Hash);

        Assert.Equal("24.2.0", schema.Version);
        Assert.Equal("abc123", schema.Build);
        Assert.Equal(Hash, schema.SourceSha256);
        IddObjectDefinition item = schema["test:object"];
        Assert.Equal("Test Group", item.Group);
        Assert.True(item.IsUnique);
        Assert.True(item.IsRequired);
        Assert.Equal(2, item.MinimumFields);
        Assert.Equal(2, item.ExtensibleGroupSize);
        Assert.Equal(2, item.ExtensibleStartIndex);
        Assert.Same(item.Fields[2], item.ResolveField(4));
        Assert.Same(item.Fields[3], item.ResolveField(5));

        IddFieldDefinition size = item["size"];
        Assert.Equal(IddFieldKind.Numeric, size.Kind);
        Assert.Equal(IddDataType.Real, size.DataType);
        Assert.Equal("m", size.Units);
        Assert.Equal("ft", size.IpUnits);
        Assert.Equal("1.5", size.DefaultValue);
        Assert.True(size.IsAutosizable);
        Assert.NotNull(size.Minimum);
        Assert.False(size.Minimum!.IsInclusive);
        Assert.Equal(0, size.Minimum.Value);
        Assert.NotNull(size.Maximum);
        Assert.True(size.Maximum!.IsInclusive);
        Assert.Equal(10, size.Maximum.Value);

        IddFieldDefinition mode = item["Mode"];
        Assert.Equal(new[] { "On", "Off" }, mode.Choices);
        Assert.True(mode.IsAutocalculatable);
        Assert.True(mode.IsDeprecated);
        Assert.Equal(new[] { "TestClasses" }, mode.ReferenceClassNames);
    }

    [Fact]
    public void ParsedCollectionsAreDefensiveAndReadOnly()
    {
        var sourceFields = new List<IddFieldDefinition>
        {
            new("A1", 0, IddFieldKind.Alpha, "Name"),
        };
        var definition = new IddObjectDefinition("Thing", "Group", sourceFields);
        sourceFields.Clear();
        var schema = new IddSchema("1", "b", Hash, new[] { definition });

        Assert.Single(definition.Fields);
        Assert.Throws<NotSupportedException>(
            () => ((IList<IddObjectDefinition>)schema.Objects).Add(definition));
        Assert.Throws<KeyNotFoundException>(() => schema["Missing"]);
    }

    [Fact]
    public void CacheRoundTripsNeutralJsonAndRejectsAnotherSourceHash()
    {
        IddSchema original = IddParser.Parse(
            "!IDD_Version 24.2.0\n\\group G\nThing,\n A1; \\field Name\n \\default X\n",
            Hash);
        using var cache = new MemoryStream();
        IddSchemaCache.Write(cache, original, leaveOpen: true);

        cache.Position = 0;
        using (var gzip = new GZipStream(cache, CompressionMode.Decompress, leaveOpen: true))
        using (var reader = new StreamReader(gzip, Encoding.UTF8, true, 1024, leaveOpen: true))
        {
            string json = reader.ReadToEnd();
            Assert.Contains("goniegonie.invisible-dragon.idd-cache.v1", json, StringComparison.Ordinal);
            Assert.Contains("source_sha256", json, StringComparison.Ordinal);
            Assert.DoesNotContain("$type", json, StringComparison.Ordinal);
        }

        cache.Position = 0;
        IddSchema restored = IddSchemaCache.Read(cache, Hash, leaveOpen: true);
        Assert.Equal(original.Version, restored.Version);
        Assert.Equal(original.SourceSha256, restored.SourceSha256);
        Assert.Equal(original["Thing"]["Name"].DefaultValue, restored["thing"]["name"].DefaultValue);

        cache.Position = 0;
        Assert.Throws<InvalidDataException>(() => IddSchemaCache.Read(cache, new string('b', 64), leaveOpen: true));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ParsesInstalledEnergyPlus242Dictionary()
    {
        string? path = EnergyPlusTestFiles.Find("Energy+.idd");
        if (path is null)
        {
            return;
        }

        IddSchema schema = IddParser.ParseFile(path);

        Assert.Equal("24.2.0", schema.Version);
        Assert.Equal("94a887817b", schema.Build);
        Assert.True(schema.Objects.Count > 800);
        Assert.True(schema.Groups.Count > 40);
        Assert.Equal("3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2", schema.SourceSha256);
        Assert.True(schema["Building"].IsRequired);
        Assert.True(schema["Building"].IsUnique);
        Assert.False(schema["Building"]["Loads Convergence Tolerance Value"].Minimum!.IsInclusive);
        Assert.Equal(1, schema["ShadowCalculation"].ExtensibleGroupSize);
        Assert.Equal(IddDataType.ObjectList, schema["BuildingSurface:Detailed"]["Zone Name"].DataType);
    }
}

internal static class EnergyPlusTestFiles
{
    public static string? Find(string relativePath)
    {
        var roots = new List<string?>
        {
            Environment.GetEnvironmentVariable("DRAGONS_ENERGYPLUS_HOME"),
            Environment.GetEnvironmentVariable("ENERGYPLUS_HOME"),
            @"C:\EnergyPlusV24-2-0",
        };

        foreach (string? root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

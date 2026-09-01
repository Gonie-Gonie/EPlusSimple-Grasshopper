using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Tests.Idd;

namespace Dragons.InvisibleDragon.Tests.Idf;

public sealed class IdfDocumentTests
{
    [Fact]
    public void ParserPreservesOrderCommentsBlankFieldsAndQuotedCommas()
    {
        IddSchema schema = TestSchema.Create();
        const string text = """
            ! preamble
            Version,
              24.2; !- Version Identifier

            ! zone comment
            Zone,
              "North, Zone", !- Name
              ; !- Size
            ! trailing
            """;

        IdfDocument document = IdfParser.Parse(text, schema);

        Assert.Equal(2, document.Count);
        Assert.Equal("preamble", document.PreambleComments[0]);
        Assert.Equal("Zone", document[1].ObjectType);
        Assert.Equal("\"North, Zone\"", document["zone"][0]["Name"]);
        Assert.Equal("- Name", document[1].Fields[0].InlineComment);
        Assert.Equal("trailing", document.TrailingComments[0]);
        Assert.Same(document[1], document["ZONE"]["North, Zone"]);
    }

    [Fact]
    public void AppendAndInsertMaintainGlobalAndTypedIndexes()
    {
        IddSchema schema = TestSchema.Create();
        var document = new IdfDocument(schema);
        var first = new IdfObject("Zone", new[] { "First", "1" });
        var second = new IdfObject("Zone", new[] { "Second", "2" });
        var version = new IdfObject("Version", new[] { "24.2" });

        document.Append(first);
        document.Insert(0, version);
        document["Zone"].Insert(0, second);

        Assert.Equal(new[] { "Version", "Zone", "Zone" }, document.Select(item => item.ObjectType));
        Assert.Same(second, document["Zone"][0]);
        Assert.Same(first, document["zone"]["first"]);
    }

    [Fact]
    public void ApplyingDefaultsRespectsMinimumFieldReach()
    {
        var document = new IdfDocument(TestSchema.Create(), new[]
        {
            new IdfObject("Version", new[] { "" }),
            new IdfObject("Zone", new[] { "Office" }),
        });

        document.ApplyDefaults();

        Assert.Equal("24.2", document["Version"][0][0]);
        Assert.Equal(2, document["Zone"][0].Count);
        Assert.Equal("5", document["Zone"][0][1]);
    }

    [Fact]
    public void WriterIsDeterministicAndSemanticallyRoundTrips()
    {
        IddSchema schema = TestSchema.Create();
        IdfDocument original = IdfParser.Parse(
            "! heading\nVersion,24.2;\nZone, Office, 1.0; ! final field\n",
            schema);

        string first = IdfWriter.Write(original);
        string second = IdfWriter.Write(original);
        IdfDocument reparsed = IdfParser.Parse(first, schema);

        Assert.Equal(first, second);
        Assert.DoesNotContain('\r', first);
        Assert.True(IdfSemanticComparer.AreEquivalent(original, reparsed));
    }

    [Fact]
    public void SemanticComparerNormalizesNumbersCaseAndTrailingBlanks()
    {
        IddSchema schema = TestSchema.Create();
        IdfDocument left = IdfParser.Parse("Version,24.2;\nZone,Office,1.0,,;", schema);
        IdfDocument right = IdfParser.Parse("version,24.2;\nzone,office,1e0;", schema);

        Assert.True(IdfSemanticComparer.Default.Equals(left, right));
        Assert.Equal(
            IdfSemanticComparer.Default.GetHashCode(left),
            IdfSemanticComparer.Default.GetHashCode(right));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void InstalledExampleIdfSemanticallyRoundTripsAgainstActualIdd()
    {
        string? iddPath = EnergyPlusTestFiles.Find("Energy+.idd");
        string? idfPath = EnergyPlusTestFiles.Find(Path.Combine("ExampleFiles", "1ZoneUncontrolled.idf"));
        if (iddPath is null || idfPath is null)
        {
            return;
        }

        IddSchema schema = IddParser.ParseFile(iddPath);
        IdfDocument original = IdfParser.ParseFile(idfPath, schema);
        string written = IdfWriter.Write(original);
        IdfDocument reparsed = IdfParser.Parse(written, schema);

        Assert.True(original.Count > 20);
        Assert.Equal("24.2", original.EnergyPlusVersion);
        Assert.True(IdfSemanticComparer.AreEquivalent(original, reparsed));
    }
}

internal static class TestSchema
{
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static IddSchema Create()
    {
        var version = new IddObjectDefinition(
            "Version",
            "Simulation",
            new[]
            {
                new IddFieldDefinition(
                    "A1",
                    0,
                    IddFieldKind.Alpha,
                    "Version Identifier",
                    defaultValue: "24.2",
                    isRequired: true),
            },
            isUnique: true,
            isRequired: true,
            minimumFields: 1);
        var zone = new IddObjectDefinition(
            "Zone",
            "Geometry",
            new[]
            {
                new IddFieldDefinition(
                    "A1",
                    0,
                    IddFieldKind.Alpha,
                    "Name",
                    isRequired: true,
                    references: new[] { "ZoneNames" }),
                new IddFieldDefinition(
                    "N1",
                    1,
                    IddFieldKind.Numeric,
                    "Size",
                    defaultValue: "5",
                    dataType: IddDataType.Real,
                    minimum: new IddNumericBound(0, false),
                    maximum: new IddNumericBound(10, true)),
            },
            minimumFields: 2);
        var surface = new IddObjectDefinition(
            "Surface",
            "Geometry",
            new[]
            {
                new IddFieldDefinition("A1", 0, IddFieldKind.Alpha, "Name", isRequired: true),
                new IddFieldDefinition(
                    "A2",
                    1,
                    IddFieldKind.Alpha,
                    "Zone Name",
                    isRequired: true,
                    dataType: IddDataType.ObjectList,
                    objectLists: new[] { "ZoneNames" }),
                new IddFieldDefinition(
                    "A3",
                    2,
                    IddFieldKind.Alpha,
                    "Mode",
                    defaultValue: "On",
                    dataType: IddDataType.Choice,
                    choices: new[] { "On", "Off" }),
            });
        return new IddSchema("24.2.0", "test", Hash, new[] { version, zone, surface });
    }
}

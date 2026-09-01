using Dragons.InvisibleDragon.Idf;

namespace Dragons.InvisibleDragon.Tests.Idf;

public sealed class IdfValidatorTests
{
    [Fact]
    public void ValidatesDefaultsRangesChoicesAndReferencesTogether()
    {
        var document = new IdfDocument(TestSchema.Create(), new[]
        {
            new IdfObject("Version", new[] { "24.2" }),
            new IdfObject("Zone", new[] { "Office", "0" }),
            new IdfObject("Surface", new[] { "Wall", "Missing Zone", "Maybe" }),
        });

        var result = IdfValidator.Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "IDF_FIELD_MINIMUM");
        Assert.Contains(result.Diagnostics, item => item.Code == "IDF_FIELD_CHOICE");
        Assert.Contains(result.Diagnostics, item => item.Code == "IDF_FIELD_REFERENCE");
    }

    [Fact]
    public void AcceptsResolvedReferenceAndInclusiveMaximum()
    {
        var document = new IdfDocument(TestSchema.Create(), new[]
        {
            new IdfObject("Version", new[] { "24.2" }),
            new IdfObject("Zone", new[] { "Office", "10" }),
            new IdfObject("Surface", new[] { "Wall", "office", "off" }),
        });

        var result = IdfValidator.Validate(document);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
    }

    [Fact]
    public void AccumulatesMissingUnknownUniqueAndRequiredFailures()
    {
        var document = new IdfDocument(TestSchema.Create(), new[]
        {
            new IdfObject("Zone", new[] { "", "not-a-number" }),
            new IdfObject("Unknown", Array.Empty<string>()),
        });

        var result = IdfValidator.Validate(document);

        Assert.Contains(result.Diagnostics, item => item.Code == "IDF_REQUIRED_OBJECT_MISSING");
        Assert.Contains(result.Diagnostics, item => item.Code == "IDF_REQUIRED_FIELD_EMPTY");
        Assert.Contains(result.Diagnostics, item => item.Code == "IDF_FIELD_NUMBER");
        Assert.Contains(result.Diagnostics, item => item.Code == "IDF_OBJECT_UNKNOWN");
    }
}

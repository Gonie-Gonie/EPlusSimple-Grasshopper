namespace Dragons.SimpleDragon.Tests;

public sealed class GrrRoundTripTests
{
    [Fact]
    public void UpstreamAshraeResultLoadsWithExactMonthlyValuesAndSummaries()
    {
        GrrReadResult read = GrrReader.ReadFile(Fixture());

        Assert.True(read.Success, Describe(read));
        Assert.Empty(read.Diagnostics);
        GreenRetrofitResult result = read.RequireResult();
        Assert.Equal("0.7.0", GrrFormat.Version);
        Assert.Equal(48d, result.TotalArea);
        Assert.Equal(3.11d, result.SiteUses[EnergyEndUse.Heating, EnergyCarrier.Electricity][0]);
        Assert.Equal(0.78d, result.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.NaturalGas][0]);
        Assert.Equal(8.55d, result.SourceUses[EnergyEndUse.Heating, EnergyCarrier.Electricity][0]);
        Assert.Equal(506.68d, result.Cost[EnergyEndUse.Heating, EnergyCarrier.Electricity][0]);
        Assert.Equal(
            79.34d,
            result.PerAreaSummaries[GreenRetrofitMetric.SiteUses].AnnualTotal);
        Assert.Equal(
            582880.8d,
            result.GrossSummaries[GreenRetrofitMetric.Cost].AnnualTotal);
        Assert.Equal(
            325.44d,
            result.GrossSummaries[GreenRetrofitMetric.SiteUses].MonthlyTotal[0]);
    }

    [Fact]
    public void WriterIsDeterministicUnicodeSafeAndFixedAfterOneRoundTrip()
    {
        GreenRetrofitResult source = GrrReader.ReadFile(Fixture()).RequireResult();

        string first = GrrWriter.Serialize(source);
        string duplicate = GrrWriter.Serialize(source);
        GrrReadResult reread = GrrReader.Read(first);

        Assert.Equal(first, duplicate);
        Assert.EndsWith("\n", first, StringComparison.Ordinal);
        Assert.Contains("\"DISTRICTHEATING\"", first, StringComparison.Ordinal);
        Assert.Contains("\"summary_per_area\"", first, StringComparison.Ordinal);
        Assert.True(reread.Success, Describe(reread));
        Assert.Equal(first, GrrWriter.Serialize(reread.RequireResult()));
    }

    [Fact]
    public void DerivedMetricsAndSummariesReproduceThePinnedUpstreamResult()
    {
        GreenRetrofitResult expected = GrrReader.ReadFile(Fixture()).RequireResult();

        GreenRetrofitResult actual = GreenRetrofitResult.FromSiteUses(
            expected.TotalArea,
            expected.SiteUses);

        foreach (GreenRetrofitMetric metric in Enum.GetValues<GreenRetrofitMetric>())
        {
            foreach (EnergyEndUse endUse in Enum.GetValues<EnergyEndUse>())
            {
                foreach (EnergyCarrier carrier in Enum.GetValues<EnergyCarrier>())
                {
                    Assert.Equal(
                        expected.Metrics[metric][endUse, carrier].ToArray(),
                        actual.Metrics[metric][endUse, carrier].ToArray());
                }
            }

            Assert.Equal(
                expected.PerAreaSummaries[metric].MonthlyTotal.ToArray(),
                actual.PerAreaSummaries[metric].MonthlyTotal.ToArray());
            Assert.Equal(
                expected.PerAreaSummaries[metric].AnnualTotal,
                actual.PerAreaSummaries[metric].AnnualTotal);
            Assert.Equal(
                expected.GrossSummaries[metric].MonthlyTotal.ToArray(),
                actual.GrossSummaries[metric].MonthlyTotal.ToArray());
            Assert.Equal(
                expected.GrossSummaries[metric].AnnualTotal,
                actual.GrossSummaries[metric].AnnualTotal);
        }
    }

    [Fact]
    public void InvalidSyntaxMissingPropertiesAndWrongMonthCountHaveStableDiagnostics()
    {
        GrrReadResult syntax = GrrReader.Read("{\"building\":");
        Assert.False(syntax.Success);
        Assert.Equal("SD.GRR.JSON_INVALID", Assert.Single(syntax.Diagnostics).Code);

        GrrReadResult missing = GrrReader.Read("{\"building\":{\"total_area\":48}}");
        Assert.False(missing.Success);
        Assert.Equal("SD.GRR.PROPERTY_REQUIRED", Assert.Single(missing.Diagnostics).Code);

        string fixture = File.ReadAllText(Fixture());
        string invalid = ReplaceFirst(
            fixture,
            "[3.11, 2.28, 1.34, 0.88, 0.16, 0.0, 0.0, 0.0, 0.06, 0.56, 1.61, 2.97]",
            "[3.11]");
        GrrReadResult months = GrrReader.Read(invalid);
        Assert.False(months.Success);
        Assert.Equal("SD.GRR.MONTH_COUNT_INVALID", Assert.Single(months.Diagnostics).Code);
    }

    private static string Fixture()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "fixtures",
                "simple-dragon",
                "grr",
                "ASHRAE 140 modified.grr");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the GRR fixture.");
    }

    private static string ReplaceFirst(string source, string oldValue, string newValue)
    {
        int index = source.IndexOf(oldValue, StringComparison.Ordinal);
        Assert.True(index >= 0, "The fixture token to replace was not found.");
        return source.Remove(index, oldValue.Length).Insert(index, newValue);
    }

    private static string Describe(GrrReadResult result)
    {
        return string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message));
    }
}

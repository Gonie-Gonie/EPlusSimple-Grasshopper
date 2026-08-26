using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;

namespace GonieGonie.InvisibleDragon.Tests.Common;

public sealed class EnergyPlusVersionTests
{
    [Fact]
    public void ConstructorsExposeThreePartReadOnlySequence()
    {
        var complete = new EnergyPlusVersion(24, 2, 1);
        var defaultPatch = new EnergyPlusVersion(24, 2);

        Assert.Equal(24, complete.Major);
        Assert.Equal(2, complete.Minor);
        Assert.Equal(1, complete.Patch);
        Assert.Equal(3, complete.Count);
        Assert.Equal(new[] { 24, 2, 1 }, complete.ToArray());
        Assert.Equal(24, complete[0]);
        Assert.Equal(2, complete[1]);
        Assert.Equal(1, complete[2]);
        Assert.Equal(0, defaultPatch.Patch);
        Assert.Throws<ArgumentOutOfRangeException>(() => complete[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => complete[3]);
    }

    [Theory]
    [InlineData("V9-6-0", 9, 6, 0)]
    [InlineData("9.6.0", 9, 6, 0)]
    [InlineData("EnergyPlus 24_2", 24, 2, 0)]
    [InlineData("release=-24--2-+0!", 24, 2, 0)]
    [InlineData("V١٢-٢-٠", 12, 2, 0)]
    [InlineData("V\U0001D7D0\U0001D7D2-\U0001D7D0-\U0001D7CE", 24, 2, 0)]
    public void StringConstructorUsesNonNumericTokenBoundaries(
        string source,
        int major,
        int minor,
        int patch)
    {
        var version = new EnergyPlusVersion(source);

        Assert.Equal(new[] { major, minor, patch }, version.ToArray());
    }

    [Fact]
    public void FormattingAndLegacyNamesUseAllThreeParts()
    {
        var version = new EnergyPlusVersion(24, 2);

        Assert.Equal("24-2-0", version.Format());
        Assert.Equal("24.2.0", version.Format("."));
        Assert.Equal("24::2::0", version.Format("::"));
        Assert.Equal("2420", version.Format(string.Empty));
        Assert.Equal("V24-2-0-Energy+.idd", version.LegacyIddFileName);
        Assert.Equal("EnergyPlusV24-2-0", version.EnergyPlusDirectoryName);
        Assert.Throws<ArgumentNullException>(() => version.Format(null!));
    }

    [Fact]
    public void FromKeepsExistingIdentityAndConvertsTypedInputs()
    {
        var existing = new EnergyPlusVersion(24, 2, 0);

        Assert.Same(existing, EnergyPlusVersion.From(existing));
        Assert.Same(existing, EnergyPlusVersion.From((IReadOnlyList<int>)existing));
        Assert.Equal(new[] { 9, 6, 0 }, EnergyPlusVersion.From("V9-6").ToArray());
        Assert.Equal(
            new[] { 9, 6, 1 },
            EnergyPlusVersion.From((IReadOnlyList<int>)new[] { 9, 6, 1 }).ToArray());

        var equalParts = new EnergyPlusVersion(24, 2, 0);
        Assert.NotSame(existing, equalParts);
        Assert.False(existing.Equals(equalParts));
    }

    [Fact]
    public void InvalidCountsOverflowAndNativeNegativesFailCleanly()
    {
        Assert.Throws<ArgumentNullException>(() => new EnergyPlusVersion(null!));
        Assert.Throws<ArgumentException>(() => new EnergyPlusVersion("24"));
        Assert.Throws<ArgumentException>(() => new EnergyPlusVersion("24.2.0.1"));
        Assert.Throws<ArgumentException>(() => new EnergyPlusVersion("no version"));
        ArgumentOutOfRangeException overflow = Assert.Throws<ArgumentOutOfRangeException>(
            () => new EnergyPlusVersion("2147483648.2.0"));
        Assert.Equal("version", overflow.ParamName);
        Assert.Equal("2147483648.2.0", overflow.ActualValue);
        Assert.Equal(int.MaxValue, new EnergyPlusVersion("2147483647.2.0").Major);

        Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyPlusVersion(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyPlusVersion(24, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyPlusVersion(24, 2, -1));
        Assert.Throws<ArgumentException>(() =>
            EnergyPlusVersion.From((IReadOnlyList<int>)new[] { 24 }));
        Assert.Throws<ArgumentException>(() =>
            EnergyPlusVersion.From((IReadOnlyList<int>)new[] { 24, 2, 0, 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EnergyPlusVersion.From((IReadOnlyList<int>)new[] { 24, -2 }));
        Assert.Throws<ArgumentNullException>(() => EnergyPlusVersion.From((EnergyPlusVersion)null!));
        Assert.Throws<ArgumentNullException>(() => EnergyPlusVersion.From((string)null!));
        Assert.Throws<ArgumentNullException>(() => EnergyPlusVersion.From((IReadOnlyList<int>)null!));
    }

    [Fact]
    public void CentralDefaultsDriveSchedulesAndGeneratedIdf()
    {
        Assert.Equal(new[] { 24, 2, 0 }, EnergyPlusDefaults.DefaultVersion.ToArray());
        Assert.Equal(EnergyPlusDefaults.DefaultYear, Schedule.DefaultYear);

        IdfDocument document = new EnergyModel(
            "defaults",
            Array.Empty<Zone>()).ToIdfDocument();
        IdfObject version = Assert.Single(document["Version"]);
        IdfObject runPeriod = Assert.Single(document["RunPeriod"]);

        Assert.Equal("24.2", version[0]);
        Assert.Equal("2026", runPeriod[3]);
        Assert.Equal("2026", runPeriod[6]);
    }
}

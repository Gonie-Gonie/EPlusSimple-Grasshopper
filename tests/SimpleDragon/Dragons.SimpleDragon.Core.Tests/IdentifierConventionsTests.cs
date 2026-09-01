using System.Globalization;
using System.Reflection;

namespace Dragons.SimpleDragon.Tests;

public sealed class IdentifierConventionsTests
{
    private static readonly (AutoIdPrefix Member, string Name, string Value)[] ExpectedAutoIdPrefixes =
    {
        (AutoIdPrefix.Material, "MATERIAL", "MTRL"),
        (AutoIdPrefix.SurfaceConstruction, "SURFACE_CONSTRUCTION", "CTSF"),
        (AutoIdPrefix.FenestrationConstruction, "FENESTRATION_CONSTRUCTION", "CTFN"),
        (AutoIdPrefix.SourceSystem, "SOURCE_SYSTEM", "SRCE"),
        (AutoIdPrefix.SupplySystem, "SUPPLY_SYSTEM", "SUPL"),
        (AutoIdPrefix.HeatExchanger, "HEAT_EXCHANGER", "ERVT"),
        (AutoIdPrefix.PvPanel, "PV_PANEL", "PVPN"),
        (AutoIdPrefix.Surface, "SURFACE", "SURF"),
        (AutoIdPrefix.Fenestration, "FENESTRATION", "FNST"),
        (AutoIdPrefix.Zone, "ZONE", "ZONE"),
        (AutoIdPrefix.DaySchedule, "DAY_SCHEDULE", "DYSC"),
        (AutoIdPrefix.Ruleset, "RULESET", "RLST"),
        (AutoIdPrefix.Schedule, "SCHEDULE", "SCHE"),
        (AutoIdPrefix.Profile, "PROFILE", "PRFL"),
    };

    private static readonly (SpecialTag Member, string Name, string Value)[] ExpectedSpecialTags =
    {
        (SpecialTag.Special, "SPECIAL", "SPECIAL"),
        (SpecialTag.Database, "DB", "FROM_DB"),
        (SpecialTag.Clone, "CLONE", "CLONE_OF"),
        (SpecialTag.Flip, "FLIP", "REVERSED"),
        (SpecialTag.CoolRoof, "COOLROOF", "FOR_COOLROOF"),
    };

    [Fact]
    public void AutoIdPrefixesPreserveExactUpstreamOrderNamesAndValues()
    {
        Assert.Equal(14, AutoIdPrefix.Values.Count);
        Assert.Equal(
            ExpectedAutoIdPrefixes.Select(item => item.Name),
            AutoIdPrefix.Values.Select(item => item.Name));
        Assert.Equal(
            ExpectedAutoIdPrefixes.Select(item => item.Value),
            AutoIdPrefix.Values.Select(item => item.Value));
        Assert.Equal(
            AutoIdPrefix.Values.Count,
            AutoIdPrefix.Values.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            AutoIdPrefix.Values.Count,
            AutoIdPrefix.Values.Select(item => item.Value).Distinct(StringComparer.Ordinal).Count());

        for (int index = 0; index < ExpectedAutoIdPrefixes.Length; index++)
        {
            Assert.Same(ExpectedAutoIdPrefixes[index].Member, AutoIdPrefix.Values[index]);
        }
    }

    [Fact]
    public void EveryAutoIdPrefixRoundTripsAndFormatsLikeUpstream()
    {
        foreach ((AutoIdPrefix member, string _, string value) in ExpectedAutoIdPrefixes)
        {
            Assert.Same(member, AutoIdPrefix.FromValue(value));
            Assert.True(AutoIdPrefix.TryFromValue(value, out AutoIdPrefix? parsed));
            Assert.Same(member, parsed);
            Assert.True(member == parsed);
            Assert.False(member != parsed);
            Assert.Equal(member.GetHashCode(), parsed!.GetHashCode());

            string plain = value + "-";
            Assert.Equal(plain, member.ToString());
            Assert.Equal(plain, member.ToString(null, CultureInfo.InvariantCulture));
            Assert.Equal(plain, member.ToString(string.Empty, CultureInfo.GetCultureInfo("ko-KR")));
            Assert.Equal(plain, $"{member}");
            Assert.Equal(plain, string.Format(CultureInfo.GetCultureInfo("fr-FR"), "{0}", member));

            Assert.Equal(value + ":SURFACE-", member.ToString("SURFACE", CultureInfo.InvariantCulture));
            Assert.Equal(value + ":SURFACE-", $"{member:SURFACE}");
            Assert.Equal(value + "::-", member.ToString(":", null));
            Assert.Equal(value + ":표면-", member.ToString("표면", CultureInfo.GetCultureInfo("en-US")));
            Assert.Equal(value + ": -", member.ToString(" ", null));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("mtrl")]
    [InlineData("MTRL ")]
    [InlineData(" MTRL")]
    [InlineData("MTRL-")]
    [InlineData("DYSC-")]
    [InlineData("SPECIAL")]
    [InlineData("SCHE:X-")]
    public void AutoIdPrefixRejectsEveryNonExactToken(string value)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => AutoIdPrefix.FromValue(value));
        Assert.Equal("value", exception.ParamName);
        Assert.Equal(value, exception.ActualValue);
        Assert.False(AutoIdPrefix.TryFromValue(value, out AutoIdPrefix? result));
        Assert.Null(result);
    }

    [Fact]
    public void AutoIdPrefixRejectsNullAndExposesAnImmutableCatalog()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => AutoIdPrefix.FromValue(null!));
        Assert.Equal("value", exception.ParamName);
        Assert.False(AutoIdPrefix.TryFromValue(null, out AutoIdPrefix? result));
        Assert.Null(result);

        AssertImmutableConventionType(AutoIdPrefix.Values, AutoIdPrefix.Material);
        Assert.False(AutoIdPrefix.Material.Equals(null));
        Assert.NotEqual(AutoIdPrefix.Material, AutoIdPrefix.Surface);
        Assert.True(AutoIdPrefix.Material != AutoIdPrefix.Surface);
    }

    [Fact]
    public void SpecialTagsPreserveExactUpstreamOrderNamesAndValues()
    {
        Assert.Equal(5, SpecialTag.Values.Count);
        Assert.Equal(
            ExpectedSpecialTags.Select(item => item.Name),
            SpecialTag.Values.Select(item => item.Name));
        Assert.Equal(
            ExpectedSpecialTags.Select(item => item.Value),
            SpecialTag.Values.Select(item => item.Value));
        Assert.Equal(
            SpecialTag.Values.Count,
            SpecialTag.Values.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            SpecialTag.Values.Count,
            SpecialTag.Values.Select(item => item.Value).Distinct(StringComparer.Ordinal).Count());

        for (int index = 0; index < ExpectedSpecialTags.Length; index++)
        {
            Assert.Same(ExpectedSpecialTags[index].Member, SpecialTag.Values[index]);
        }
    }

    [Fact]
    public void EverySpecialTagRoundTripsAndFormatsLikeUpstream()
    {
        foreach ((SpecialTag member, string _, string value) in ExpectedSpecialTags)
        {
            Assert.Same(member, SpecialTag.FromValue(value));
            Assert.True(SpecialTag.TryFromValue(value, out SpecialTag? parsed));
            Assert.Same(member, parsed);
            Assert.True(member == parsed);
            Assert.False(member != parsed);
            Assert.Equal(member.GetHashCode(), parsed!.GetHashCode());

            string plain = "$" + value + "$:";
            Assert.Equal(plain, member.ToString());
            Assert.Equal(plain, member.ToString(null, CultureInfo.InvariantCulture));
            Assert.Equal(plain, member.ToString(string.Empty, CultureInfo.GetCultureInfo("ko-KR")));
            Assert.Equal(plain, $"{member}");
            Assert.Equal(plain, string.Format(CultureInfo.GetCultureInfo("fr-FR"), "{0}", member));

            Assert.Equal("$" + value + ":SURFACE$:", member.ToString("SURFACE", CultureInfo.InvariantCulture));
            Assert.Equal("$" + value + ":SURFACE$:", $"{member:SURFACE}");
            Assert.Equal("$" + value + "::$:", member.ToString(":", null));
            Assert.Equal("$" + value + ":표면$:", member.ToString("표면", CultureInfo.GetCultureInfo("en-US")));
            Assert.Equal("$" + value + ": $:", member.ToString(" ", null));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("from_db")]
    [InlineData("FROM_DB ")]
    [InlineData(" FROM_DB")]
    [InlineData("$FROM_DB$:")]
    [InlineData("DB")]
    [InlineData("COOLROOF")]
    [InlineData("MTRL")]
    public void SpecialTagRejectsEveryNonExactToken(string value)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => SpecialTag.FromValue(value));
        Assert.Equal("value", exception.ParamName);
        Assert.Equal(value, exception.ActualValue);
        Assert.False(SpecialTag.TryFromValue(value, out SpecialTag? result));
        Assert.Null(result);
    }

    [Fact]
    public void SpecialTagRejectsNullAndExposesAnImmutableCatalog()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => SpecialTag.FromValue(null!));
        Assert.Equal("value", exception.ParamName);
        Assert.False(SpecialTag.TryFromValue(null, out SpecialTag? result));
        Assert.Null(result);

        AssertImmutableConventionType(SpecialTag.Values, SpecialTag.Special);
        Assert.False(SpecialTag.Special.Equals(null));
        Assert.NotEqual(SpecialTag.Special, SpecialTag.Database);
        Assert.True(SpecialTag.Special != SpecialTag.Database);
    }

    private static void AssertImmutableConventionType<T>(IReadOnlyList<T> values, T sample)
        where T : class
    {
        Type type = typeof(T);
        Assert.True(type.IsSealed);
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
            property => Assert.Null(property.SetMethod));

        IList<T> mutableView = Assert.IsAssignableFrom<IList<T>>(values);
        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(sample));
        Assert.Throws<NotSupportedException>(() => mutableView.RemoveAt(0));
    }
}

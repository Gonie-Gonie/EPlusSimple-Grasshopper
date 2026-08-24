using System.Globalization;
using System.Text.Json;

namespace GonieGonie.BuildingEnergy.Contracts.Tests;

public sealed class EntityIdTests
{
    [Fact]
    public void ValueObjectUsesOrdinalValueEqualityAndOrdering()
    {
        EntityId first = new("ZONE-000001");
        EntityId same = new("ZONE-000001");
        EntityId second = new("ZONE-000002");

        Assert.Equal(first, same);
        Assert.NotEqual(first, second);
        Assert.True(first < second);
        Assert.Equal("ZONE-000001", first.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" ZONE-000001")]
    [InlineData("ZONE 000001")]
    [InlineData("ZONE-000001\n")]
    public void InvalidValuesAreRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new EntityId(value));
    }

    [Fact]
    public void JsonRepresentsAnIdentifierAsAScalarString()
    {
        JsonSerializerOptions options = BuildingEnergyJson.CreateOptions();
        EntityId original = new("SURF-ZONE-000001-000007");

        string json = JsonSerializer.Serialize(original, options);
        EntityId? restored = JsonSerializer.Deserialize<EntityId>(json, options);

        Assert.Equal("\"SURF-ZONE-000001-000007\"", json);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void GeneratorUsesInvariantPaddedSequences()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            DeterministicIdGenerator generator = new("ZONE");

            Assert.Equal(new EntityId("ZONE-000001"), generator.Next());
            Assert.Equal(new EntityId("ZONE-000002"), generator.Next());
            Assert.Equal(new EntityId("ZONE-000042"), generator.At(42));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void CapturedStateResumesAtExactlyTheNextIdentifier()
    {
        DeterministicIdGenerator original = new("MTRL", firstSequence: 8, minimumDigits: 4);
        Assert.Equal("MTRL-0008", original.Next().Value);

        DeterministicIdGenerator restored = new(original.CaptureState());

        Assert.Equal("MTRL-0009", restored.Next().Value);
        Assert.Equal(10, restored.NextSequence);
    }

    [Fact]
    public void ScopedGeneratorIncludesTheStableParentIdentifier()
    {
        DeterministicIdGenerator generator = DeterministicIdGenerator.ForScope(
            "SURF",
            new EntityId("ZONE-000003"));

        Assert.Equal("SURF-ZONE-000003-000001", generator.Next().Value);
    }

    [Theory]
    [InlineData("ZONE-", 1, 6)]
    [InlineData("ZONE NAME", 1, 6)]
    [InlineData("ZONE", -1, 6)]
    [InlineData("ZONE", 1, 0)]
    [InlineData("ZONE", 1, 33)]
    public void InvalidGeneratorConfigurationIsRejected(
        string prefix,
        long firstSequence,
        int minimumDigits)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new DeterministicIdGenerator(prefix, firstSequence, minimumDigits));
    }
}

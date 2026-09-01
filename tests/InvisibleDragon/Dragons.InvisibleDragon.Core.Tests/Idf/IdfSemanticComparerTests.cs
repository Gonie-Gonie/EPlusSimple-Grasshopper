using Dragons.InvisibleDragon.Idf;

namespace Dragons.InvisibleDragon.Tests.Idf;

public sealed class IdfSemanticComparerTests
{
    [Fact]
    public void ObjectOrderDoesNotAffectEqualityOrHashCode()
    {
        IdfDocument expected = Parse(
            "Version,24.2;\n" +
            "Zone,Office,1;\n" +
            "Surface,Office Wall,Office,On;\n" +
            "Zone,Lab,2;\n" +
            "Surface,Lab Wall,Lab,Off;");
        IdfDocument actual = Parse(
            "Surface,Lab Wall,Lab,off;\n" +
            "Zone,Lab,2e0;\n" +
            "Version,24.2;\n" +
            "Surface,Office Wall,Office,on;\n" +
            "Zone,Office,1.0;");

        IdfSemanticComparisonResult result = IdfSemanticComparer.Default.Compare(expected, actual);

        Assert.True(result.AreEquivalent);
        Assert.Empty(result.Mismatches);
        Assert.True(IdfSemanticComparer.Default.Equals(expected, actual));
        Assert.Equal(
            IdfSemanticComparer.Default.GetHashCode(expected),
            IdfSemanticComparer.Default.GetHashCode(actual));
    }

    [Fact]
    public void UnnamedDuplicateObjectTypesUseOrderIndependentMatching()
    {
        IdfDocument expected = Parse("Version,24.2;\nVersion,23.1;");
        IdfDocument actual = Parse("Version,23.1;\nVersion,24.2;");

        Assert.True(IdfSemanticComparer.AreEquivalent(expected, actual));
    }

    [Fact]
    public void AbsoluteAndRelativeNumericTolerancesAreCombined()
    {
        IdfDocument smallExpected = Parse("Zone,Office,0.0001;");
        IdfDocument smallActual = Parse("Zone,Office,0.0006;");
        IdfDocument largeExpected = Parse("Zone,Office,1000000;");
        IdfDocument largeActual = Parse("Zone,Office,1000001;");
        IdfDocument combinedExpected = Parse("Zone,Office,100;");
        IdfDocument combinedActual = Parse("Zone,Office,101.1;");
        var tolerant = new IdfSemanticComparer(absoluteTolerance: 0.0005, relativeTolerance: 0.000001);
        var strict = new IdfSemanticComparer(absoluteTolerance: 0.0001, relativeTolerance: 0.0000001);
        var combined = new IdfSemanticComparer(absoluteTolerance: 0.2, relativeTolerance: 0.009);

        Assert.True(tolerant.Equals(smallExpected, smallActual));
        Assert.True(tolerant.Equals(largeExpected, largeActual));
        Assert.True(combined.Equals(combinedExpected, combinedActual));
        Assert.Equal(tolerant.GetHashCode(smallExpected), tolerant.GetHashCode(smallActual));
        Assert.False(strict.Equals(smallExpected, smallActual));
        Assert.False(strict.Equals(largeExpected, largeActual));
    }

    [Theory]
    [InlineData(-0.1, 0)]
    [InlineData(0, -0.1)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void InvalidNumericTolerancesAreRejected(double absolute, double relative)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IdfSemanticComparer(absolute, relative));
    }

    [Fact]
    public void CanonicalizerMapsGeneratedObjectNamesAndReferencesTogether()
    {
        IdfDocument expected = Parse(
            "Zone,AUTO_ZONE_91,1;\n" +
            "Surface,AUTO_SURFACE_12,AUTO_ZONE_91,On;");
        IdfDocument actual = Parse(
            "Surface,GEN_SURFACE_7,GEN_ZONE_42,On;\n" +
            "Zone,GEN_ZONE_42,1;");
        var canonicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AUTO_ZONE_91"] = "ZONE#1",
            ["GEN_ZONE_42"] = "ZONE#1",
            ["AUTO_SURFACE_12"] = "SURFACE#1",
            ["GEN_SURFACE_7"] = "SURFACE#1",
        };
        var seenRoles = new HashSet<IdfSemanticValueRole>();
        var comparer = new IdfSemanticComparer(
            absoluteTolerance: 0,
            relativeTolerance: 0,
            valueCanonicalizer: context =>
            {
                seenRoles.Add(context.Role);
                return context.Role != IdfSemanticValueRole.Value &&
                    canonicalNames.TryGetValue(context.Value, out string? canonical)
                        ? canonical
                        : context.Value;
            });

        Assert.False(IdfSemanticComparer.Default.Equals(expected, actual));
        Assert.True(comparer.Equals(expected, actual));
        Assert.Contains(IdfSemanticValueRole.ObjectIdentity, seenRoles);
        Assert.Contains(IdfSemanticValueRole.ObjectReference, seenRoles);
    }

    [Fact]
    public void StructuredMismatchUsesCanonicalObjectIdentityAndFieldMetadata()
    {
        IdfDocument expected = Parse(
            "Zone,Office,1;\n" +
            "Zone,Lab,2;\n" +
            "Surface,Office Wall,Office,On;");
        IdfDocument actual = Parse(
            "Surface,Office Wall,Office,Off;\n" +
            "Zone,Lab,2;\n" +
            "Zone,Office,1.5;");

        IdfSemanticComparisonResult result = IdfSemanticComparer.Default.Compare(expected, actual);

        Assert.False(result.AreEquivalent);
        Assert.Collection(
            result.Mismatches,
            mismatch =>
            {
                Assert.Equal(IdfSemanticMismatchKind.FieldValue, mismatch.Kind);
                Assert.Equal("$.objects[Surface:OFFICE WALL].fields[2:Mode]", mismatch.Path);
                Assert.Equal("On", mismatch.Expected);
                Assert.Equal("Off", mismatch.Actual);
            },
            mismatch =>
            {
                Assert.Equal(IdfSemanticMismatchKind.FieldValue, mismatch.Kind);
                Assert.Equal("$.objects[Zone:OFFICE].fields[1:Size]", mismatch.Path);
                Assert.Equal("1", mismatch.Expected);
                Assert.Equal("1.5", mismatch.Actual);
                Assert.Contains("expected '1'", mismatch.ToString(), StringComparison.Ordinal);
            });
    }

    [Fact]
    public void StructuredMismatchReportsCountsAndMissingObjects()
    {
        IdfDocument expected = Parse("Zone,Office,1;\nZone,Lab,2;");
        IdfDocument actual = Parse("Zone,Office,1;");

        IdfSemanticComparisonResult result = IdfSemanticComparer.Default.Compare(expected, actual);

        Assert.False(result.AreEquivalent);
        Assert.Contains(
            result.Mismatches,
            mismatch => mismatch.Kind == IdfSemanticMismatchKind.ObjectCount &&
                mismatch.Path == "$.objects[Zone].count" &&
                mismatch.Expected == "2" &&
                mismatch.Actual == "1");
        Assert.Contains(
            result.Mismatches,
            mismatch => mismatch.Kind == IdfSemanticMismatchKind.MissingObject &&
                mismatch.Path == "$.objects[Zone:LAB]" &&
                mismatch.Expected == "Zone:Lab" &&
                mismatch.Actual is null);
    }

    [Fact]
    public void NullDocumentsProduceAComputerReadableRootMismatch()
    {
        IdfSemanticComparisonResult result = IdfSemanticComparer.Default.Compare(new IdfDocument(), null);

        IdfSemanticMismatch mismatch = Assert.Single(result.Mismatches);
        Assert.Equal(IdfSemanticMismatchKind.Document, mismatch.Kind);
        Assert.Equal("$", mismatch.Path);
        Assert.Equal("document", mismatch.Expected);
        Assert.Null(mismatch.Actual);
    }

    private static IdfDocument Parse(string text)
    {
        return IdfParser.Parse(text, TestSchema.Create());
    }
}

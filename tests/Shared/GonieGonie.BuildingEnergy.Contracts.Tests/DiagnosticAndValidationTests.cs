namespace GonieGonie.BuildingEnergy.Contracts.Tests;

public sealed class DiagnosticAndValidationTests
{
    private static readonly string[] ExpectedCombinedCodes =
    {
        "ZERO-AREA",
        "NON-PLANAR",
        "MISSING-REFERENCE",
    };

    [Fact]
    public void DiagnosticCarriesTypedObjectAndGeometryContext()
    {
        Guid sourceId = Guid.Parse("d2719c66-8bf2-4e58-84b0-1ef7d2be80ad");
        GeometryProvenance provenance = new(
            sourceId,
            3,
            "sha256:geometry",
            "{0;2}",
            4);
        Diagnostic diagnostic = new(
            "SIMPLEDRAGON-SURFACE-001",
            DiagnosticSeverity.Error,
            "Opening area exceeds its host surface.",
            new EntityId("SURF-000004"),
            provenance,
            "Reduce the opening area.");

        Assert.True(diagnostic.IsFailure);
        Assert.Equal(new EntityId("SURF-000004"), diagnostic.ObjectId);
        Assert.Equal(sourceId, diagnostic.Geometry!.RhinoObjectId);
        Assert.Equal(3, diagnostic.Geometry.BrepFaceIndex);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, -1)]
    public void GeometryProvenanceRejectsNegativeIndices(
        int? brepFaceIndex,
        int? grasshopperIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GeometryProvenance(
                null,
                brepFaceIndex,
                "fingerprint",
                null,
                grasshopperIndex));
    }

    [Fact]
    public void WarningsDoNotInvalidateAResult()
    {
        ValidationResult result = ValidationResult.From(
            new[]
            {
                CreateDiagnostic("INFO", DiagnosticSeverity.Info),
                CreateDiagnostic("WARN", DiagnosticSeverity.Warning),
            });

        Assert.True(result.IsValid);
        Assert.True(result.HasWarnings);
        Assert.Equal(DiagnosticSeverity.Warning, result.HighestSeverity);
    }

    [Fact]
    public void CombineAccumulatesAllErrorsWithoutShortCircuiting()
    {
        ValidationResult geometry = ValidationResult.From(
            new[]
            {
                CreateDiagnostic("ZERO-AREA", DiagnosticSeverity.Error),
                CreateDiagnostic("NON-PLANAR", DiagnosticSeverity.Error),
            });
        ValidationResult references = ValidationResult.From(
            new[]
            {
                CreateDiagnostic("MISSING-REFERENCE", DiagnosticSeverity.Fatal),
            });

        ValidationResult combined = ValidationResult.Combine(
            ValidationResult.Success,
            geometry,
            references);

        Assert.False(combined.IsValid);
        Assert.Equal(3, combined.Diagnostics.Count);
        Assert.Equal(
            ExpectedCombinedCodes,
            combined.Diagnostics.Select(diagnostic => diagnostic.Code));
        Assert.Equal(DiagnosticSeverity.Fatal, combined.HighestSeverity);
    }

    [Fact]
    public void ResultDefensivelyCopiesTheSourceCollection()
    {
        List<Diagnostic> source = new()
        {
            CreateDiagnostic("FIRST", DiagnosticSeverity.Info),
        };
        ValidationResult result = new(source);

        source.Add(CreateDiagnostic("LATE", DiagnosticSeverity.Fatal));

        Assert.Single(result.Diagnostics);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void AddReturnsANewResultAndLeavesTheOriginalUnchanged()
    {
        ValidationResult original = ValidationResult.Success;
        ValidationResult updated = original.Add(
            CreateDiagnostic("BAD-FUEL", DiagnosticSeverity.Error));

        Assert.Empty(original.Diagnostics);
        Assert.Single(updated.Diagnostics);
        Assert.False(updated.IsValid);
    }

    private static Diagnostic CreateDiagnostic(string code, DiagnosticSeverity severity)
    {
        return new Diagnostic(code, severity, "Test diagnostic.");
    }
}

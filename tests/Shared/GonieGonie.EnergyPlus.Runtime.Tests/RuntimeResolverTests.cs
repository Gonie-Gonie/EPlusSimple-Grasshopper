namespace GonieGonie.EnergyPlus.Runtime.Tests;

public sealed class RuntimeResolverTests
{
    [Fact]
    public async Task ResolvesOnlyAfterAllPayloadHashesMatch()
    {
        using var directory = new TestDirectory();
        var (runtime, manifest) = await TestRuntimeFactory.CreateAsync(directory);

        Assert.Equal(manifest, runtime.Manifest);
        Assert.Equal(System.IO.Path.Combine(runtime.RootPath, "energyplus.exe"), runtime.EnergyPlusExecutablePath);
        Assert.Equal(
            System.IO.Path.Combine(runtime.RootPath, "Energy+.schema.epJSON"),
            runtime.SchemaPath);
        Assert.True(runtime.VerifiedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task RejectsPayloadTamperingAsIntegrityFailure()
    {
        using var directory = new TestDirectory();
        var (runtime, manifest) = await TestRuntimeFactory.CreateAsync(directory);
        await File.AppendAllTextAsync(runtime.EnergyPlusExecutablePath, "tampered");

        var resolution = await new RuntimeResolver(manifest).ResolveAsync(new EnergyPlusRuntimeResolveOptions
        {
            RuntimeRoot = runtime.RootPath,
            SearchDefaultInstallLocation = false,
            SearchEnvironmentVariables = false
        });

        Assert.False(resolution.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.RuntimeIntegrity, resolution.Failure?.Category);
        Assert.Equal("RUNTIME_HASH_MISMATCH", resolution.Failure?.Code);
    }

    [Theory]
    [InlineData(false, "RUNTIME_FILE_MISSING")]
    [InlineData(true, "RUNTIME_HASH_MISMATCH")]
    public async Task RejectsMissingOrTamperedEpJsonSchemaAsIntegrityFailure(
        bool tamperInsteadOfDelete,
        string expectedFailureCode)
    {
        using var directory = new TestDirectory();
        var (runtime, manifest) = await TestRuntimeFactory.CreateAsync(directory);
        if (tamperInsteadOfDelete)
        {
            await File.AppendAllTextAsync(runtime.SchemaPath, "tampered");
        }
        else
        {
            File.Delete(runtime.SchemaPath);
        }

        var resolution = await new RuntimeResolver(manifest).ResolveAsync(new EnergyPlusRuntimeResolveOptions
        {
            RuntimeRoot = runtime.RootPath,
            SearchDefaultInstallLocation = false,
            SearchEnvironmentVariables = false
        });

        Assert.False(resolution.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.RuntimeIntegrity, resolution.Failure?.Category);
        Assert.Equal(expectedFailureCode, resolution.Failure?.Code);
        Assert.Contains("Energy+.schema.epJSON", resolution.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitMissingRootDoesNotFallBack()
    {
        using var directory = new TestDirectory();
        var missing = System.IO.Path.Combine(directory.Path, "missing");

        var resolution = await new RuntimeResolver().ResolveAsync(new EnergyPlusRuntimeResolveOptions
        {
            RuntimeRoot = missing
        });

        Assert.False(resolution.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.RuntimeNotFound, resolution.Failure?.Category);
        Assert.Single(resolution.AttemptedRoots);
    }

    [Fact]
    public async Task RejectsManifestThatDoesNotMatchPinnedPayload()
    {
        using var directory = new TestDirectory();
        var (_, fakeManifest) = await TestRuntimeFactory.CreateAsync(directory);
        var manifestPath = directory.WriteFile("manifest.json", EnergyPlusRuntimeManifest.Supported.ToJson());
        var runtimeRoot = System.IO.Path.Combine(directory.Path, "runtime");

        var resolution = await new RuntimeResolver(fakeManifest).ResolveAsync(new EnergyPlusRuntimeResolveOptions
        {
            RuntimeRoot = runtimeRoot,
            ManifestPath = manifestPath,
            SearchDefaultInstallLocation = false,
            SearchEnvironmentVariables = false
        });

        Assert.False(resolution.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.RuntimeIntegrity, resolution.Failure?.Category);
        Assert.Equal("MANIFEST_MISMATCH", resolution.Failure?.Code);
    }

    [Fact]
    public async Task MalformedCallerPathIsAUserInputFailure()
    {
        var resolution = await new RuntimeResolver().ResolveAsync(new EnergyPlusRuntimeResolveOptions
        {
            RuntimeRoot = "\0"
        });

        Assert.False(resolution.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.UserInput, resolution.Failure?.Category);
        Assert.Equal("RUNTIME_ROOT_INVALID", resolution.Failure?.Code);
    }
}

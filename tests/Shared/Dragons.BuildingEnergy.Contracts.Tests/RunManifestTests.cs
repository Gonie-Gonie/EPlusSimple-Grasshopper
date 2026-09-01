using System.Reflection;
using System.Text.Json;

namespace Dragons.BuildingEnergy.Contracts.Tests;

public sealed class RunManifestTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 8, 24, 12, 30, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedRequirementKeys = { "invisible_dragon_core_api", "energyplus" };

    [Fact]
    public void CompatibilityMetadataPinsUpstreamAndOrderedRequirements()
    {
        CompatibilityMetadata metadata = CreateCompatibility();

        Assert.Equal("SimpleDragon.GH", metadata.ProductName);
        Assert.Equal(CompatibilityIdentity.Current.UpstreamCommit, metadata.UpstreamCommit);
        Assert.Equal(
            ExpectedRequirementKeys,
            metadata.Requirements.Keys);
        Assert.Equal("0.2", metadata.Requirements["invisible_dragon_core_api"]);
        Assert.Equal("24.2.0", metadata.Requirements["energyplus"]);
    }

    [Fact]
    public void RunningManifestCompletesByReturningATerminalCopy()
    {
        RunManifest running = CreateRunningManifest();
        DateTimeOffset completedAt = StartedAt.AddMinutes(3);

        RunManifest completed = running.Complete(RunStatus.Succeeded, completedAt);

        Assert.Equal(RunStatus.Running, running.Status);
        Assert.Null(running.CompletedAtUtc);
        Assert.Equal(RunStatus.Succeeded, completed.Status);
        Assert.Equal(completedAt, completed.CompletedAtUtc);
        Assert.Equal(running.ContentHashes, completed.ContentHashes);
    }

    [Fact]
    public void ManifestJsonIsStableAndRoundTrips()
    {
        JsonSerializerOptions options = BuildingEnergyJson.CreateOptions();
        RunManifest original = CreateRunningManifest().Complete(
            RunStatus.Failed,
            StartedAt.AddMinutes(1));

        string firstJson = JsonSerializer.Serialize(original, options);
        RunManifest? restored = JsonSerializer.Deserialize<RunManifest>(firstJson, options);
        string secondJson = JsonSerializer.Serialize(restored, options);

        Assert.NotNull(restored);
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(new EntityId("RUN-000001"), restored.RunId);
        Assert.Equal(RunStatus.Failed, restored.Status);
        Assert.True(
            firstJson.IndexOf("\"schema_version\"", StringComparison.Ordinal)
            < firstJson.IndexOf("\"run_id\"", StringComparison.Ordinal));
        Assert.Contains("\"status\":\"failed\"", firstJson);
        Assert.Contains("\"energyplus_exe\":\"sha256:exe\"", firstJson);
    }

    [Fact]
    public void ConstructorRejectsInconsistentLifecycleState()
    {
        CompatibilityMetadata compatibility = CreateCompatibility();

        Assert.Throws<ArgumentException>(
            () => new RunManifest(
                RunManifest.CurrentSchemaVersion,
                new EntityId("RUN-000001"),
                new EntityId("CASE-000001"),
                "sha256:run-key",
                StartedAt,
                null,
                RunStatus.Succeeded,
                compatibility,
                new OrderedMap<string>()));
    }

    [Fact]
    public void ManifestRequiresUtcTimestamps()
    {
        DateTimeOffset localTimestamp = new(2026, 8, 24, 21, 30, 0, TimeSpan.FromHours(9));

        Assert.Throws<ArgumentException>(
            () => RunManifest.Start(
                new EntityId("RUN-000001"),
                new EntityId("CASE-000001"),
                "sha256:run-key",
                localTimestamp,
                CreateCompatibility()));
    }

    [Fact]
    public void ContractsAssemblyAndProvenanceDoNotReferenceRhinoTypes()
    {
        Assembly contractsAssembly = typeof(GeometryProvenance).Assembly;

        Assert.DoesNotContain(
            contractsAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, "RhinoCommon", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(GeometryProvenance).GetProperties(),
            property => property.PropertyType.FullName!.StartsWith("Rhino.", StringComparison.Ordinal));
    }

    private static RunManifest CreateRunningManifest()
    {
        OrderedMap<string> hashes = new OrderedMap<string>()
            .Add("model", "sha256:model")
            .Add("energyplus_exe", "sha256:exe")
            .Add("idd", "sha256:idd")
            .Add("epw", "sha256:epw");

        return RunManifest.Start(
            new EntityId("RUN-000001"),
            new EntityId("CASE-000042"),
            "sha256:run-key",
            StartedAt,
            CreateCompatibility(),
            hashes);
    }

    private static CompatibilityMetadata CreateCompatibility()
    {
        return CompatibilityMetadata.FromIdentity(
            "SimpleDragon.GH",
            "0.2.0",
            CompatibilityIdentity.Current,
            "0.2");
    }
}

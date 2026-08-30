using System.Globalization;

namespace GonieGonie.EnergyPlus.Runtime.Tests;

public sealed class RuntimeManifestTests
{
    [Fact]
    public void SupportedManifestHasPinnedIdentity()
    {
        var manifest = EnergyPlusRuntimeManifest.Supported;

        Assert.Equal("goniegonie.energyplus-runtime.v3", manifest.RuntimeSchema);
        Assert.Equal("24.2.0", manifest.EnergyPlusVersion);
        Assert.Equal("94a887817b", manifest.EnergyPlusBuild);
        Assert.Empty(manifest.Validate());
        Assert.Equal(EnergyPlusRuntimeIdentity.Supported.EnergyPlusExecutableSha256, manifest.EnergyPlusExecutableSha256);
        Assert.Equal(EnergyPlusRuntimeIdentity.Supported.IddSha256, manifest.EnergyPlusIddSha256);
        Assert.Equal(
            "aefb16d63495d170468ecab3c935f1aeb68eb07c6551403dd11cbba61cb136fa",
            manifest.EnergyPlusEpJsonSchemaSha256);
        Assert.Equal(EnergyPlusRuntimeIdentity.Supported.ExpandObjectsSha256, manifest.ExpandObjectsSha256);
        Assert.Equal(EnergyPlusRuntimeManifest.UserSuppliedWeatherPolicy, manifest.WeatherPolicy);
    }

    [Fact]
    public void JsonRoundTripPreservesManifestWithInvariantNumbers()
    {
        using var directory = new TestDirectory();
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-SA");
            var json = EnergyPlusRuntimeManifest.Supported.ToJson();
            var path = directory.WriteFile("manifest.json", json);

            Assert.Contains("179248139", json, StringComparison.Ordinal);
            Assert.Contains("energyplus_epjson_schema_sha256", json, StringComparison.Ordinal);
            Assert.Equal(EnergyPlusRuntimeManifest.Supported, EnergyPlusRuntimeManifest.Load(path));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void PinnedTemplateMatchesSupportedManifest()
    {
        var manifestPath = System.IO.Path.Combine(
            TestDirectory.FindRepositoryRoot(),
            "resources",
            "runtime",
            "manifest.template.json");

        Assert.Equal(
            EnergyPlusRuntimeManifest.Supported,
            EnergyPlusRuntimeManifest.Load(manifestPath));
    }

    [Fact]
    public void StructuralValidationReportsMalformedHash()
    {
        var manifest = EnergyPlusRuntimeManifest.Supported with
        {
            EnergyPlusExecutableSha256 = "not-a-hash"
        };

        var errors = manifest.Validate();

        Assert.Contains(errors, error => error.StartsWith("energyplus_exe_sha256", StringComparison.Ordinal));
    }

    [Fact]
    public void StructuralValidationReportsMalformedEpJsonSchemaHash()
    {
        var manifest = EnergyPlusRuntimeManifest.Supported with
        {
            EnergyPlusEpJsonSchemaSha256 = "not-a-hash"
        };

        var errors = manifest.Validate();

        Assert.Contains(
            errors,
            error => error.StartsWith("energyplus_epjson_schema_sha256", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyManifestLoadsAsRuntimeOnlySchemaWithUserSuppliedWeather()
    {
        using var directory = new TestDirectory();
        var supported = EnergyPlusRuntimeManifest.Supported;
        var legacyJson = $$"""
            {
              "runtime_schema": "goniegonie.energyplus-runtime.v1",
              "energyplus_version": "{{supported.EnergyPlusVersion}}",
              "energyplus_build": "{{supported.EnergyPlusBuild}}",
              "energyplus_archive_sha256": "{{supported.EnergyPlusArchiveSha256}}",
              "energyplus_archive_size": {{supported.EnergyPlusArchiveSize}},
              "energyplus_exe_sha256": "{{supported.EnergyPlusExecutableSha256}}",
              "energyplus_idd_sha256": "{{supported.EnergyPlusIddSha256}}",
              "expandobjects_sha256": "{{supported.ExpandObjectsSha256}}",
              "weather_pack_version": "legacy-unverified-weather",
              "weather_pack_sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "created_by": "{{supported.CreatedBy}}"
            }
            """;
        var path = directory.WriteFile("legacy-manifest.json", legacyJson);

        var loaded = EnergyPlusRuntimeManifest.Load(path);

        Assert.Equal(EnergyPlusRuntimeManifest.SupportedSchema, loaded.RuntimeSchema);
        Assert.Equal(EnergyPlusRuntimeManifest.UserSuppliedWeatherPolicy, loaded.WeatherPolicy);
        Assert.Equal(supported, loaded);
        Assert.DoesNotContain("weather_pack", loaded.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void VersionTwoManifestWithoutEpJsonHashMigratesOnlyForSupportedBuild()
    {
        using var directory = new TestDirectory();
        var supported = EnergyPlusRuntimeManifest.Supported;
        var legacyJson = $$"""
            {
              "runtime_schema": "goniegonie.energyplus-runtime.v2",
              "energyplus_version": "{{supported.EnergyPlusVersion}}",
              "energyplus_build": "{{supported.EnergyPlusBuild}}",
              "energyplus_archive_sha256": "{{supported.EnergyPlusArchiveSha256}}",
              "energyplus_archive_size": {{supported.EnergyPlusArchiveSize}},
              "energyplus_exe_sha256": "{{supported.EnergyPlusExecutableSha256}}",
              "energyplus_idd_sha256": "{{supported.EnergyPlusIddSha256}}",
              "expandobjects_sha256": "{{supported.ExpandObjectsSha256}}",
              "weather_policy": "{{supported.WeatherPolicy}}",
              "created_by": "{{supported.CreatedBy}}"
            }
            """;
        var path = directory.WriteFile("version-two-manifest.json", legacyJson);

        var loaded = EnergyPlusRuntimeManifest.Load(path);

        Assert.Equal(supported, loaded);
        Assert.Contains("energyplus_epjson_schema_sha256", loaded.ToJson(), StringComparison.Ordinal);
    }
}

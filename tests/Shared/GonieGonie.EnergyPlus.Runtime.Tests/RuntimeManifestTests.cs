using System.Globalization;

namespace GonieGonie.EnergyPlus.Runtime.Tests;

public sealed class RuntimeManifestTests
{
    [Fact]
    public void SupportedManifestHasPinnedIdentity()
    {
        var manifest = EnergyPlusRuntimeManifest.Supported;

        Assert.Equal("goniegonie.energyplus-runtime.v1", manifest.RuntimeSchema);
        Assert.Equal("24.2.0", manifest.EnergyPlusVersion);
        Assert.Equal("94a887817b", manifest.EnergyPlusBuild);
        Assert.Empty(manifest.Validate());
        Assert.Equal(EnergyPlusRuntimeIdentity.Supported.EnergyPlusExecutableSha256, manifest.EnergyPlusExecutableSha256);
        Assert.Equal(EnergyPlusRuntimeIdentity.Supported.IddSha256, manifest.EnergyPlusIddSha256);
        Assert.Equal(EnergyPlusRuntimeIdentity.Supported.ExpandObjectsSha256, manifest.ExpandObjectsSha256);
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
            Assert.Equal(EnergyPlusRuntimeManifest.Supported, EnergyPlusRuntimeManifest.Load(path));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
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
}

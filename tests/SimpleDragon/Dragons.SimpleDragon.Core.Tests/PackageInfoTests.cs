namespace Dragons.SimpleDragon.Tests;

public sealed class PackageInfoTests
{
    [Fact]
    public void PackageHasIndependentVersionAndUpstreamCompatibility()
    {
        Assert.Equal("0.1.1", PackageInfo.Version);
        Assert.Equal("0.7.0", PackageInfo.Compatibility.UpstreamVersion);
    }
}

namespace Dragons.InvisibleDragon.Tests;

public sealed class PackageInfoTests
{
    [Fact]
    public void PackageHasIndependentVersionAndUpstreamCompatibility()
    {
        Assert.Equal("0.1.2", PackageInfo.Version);
        Assert.Equal("0.7.0", PackageInfo.Compatibility.UpstreamVersion);
    }
}

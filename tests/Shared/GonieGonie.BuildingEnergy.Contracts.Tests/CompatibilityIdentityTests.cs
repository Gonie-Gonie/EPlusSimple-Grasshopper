namespace GonieGonie.BuildingEnergy.Contracts.Tests;

public sealed class CompatibilityIdentityTests
{
    [Fact]
    public void CurrentIdentityPinsThePlannedUpstreamCommit()
    {
        Assert.Equal(
            "847b01f68f438f560a986072bcaa7768fbf67897",
            CompatibilityIdentity.Current.UpstreamCommit);
        Assert.Equal("24.2.0", CompatibilityIdentity.Current.EnergyPlusVersion);
    }
}

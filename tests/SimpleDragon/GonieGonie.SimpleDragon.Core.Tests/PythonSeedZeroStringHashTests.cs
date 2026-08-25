using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class PythonSeedZeroStringHashTests
{
    [Theory]
    [InlineData("", 0L)]
    [InlineData("a", 4644417185603328019L)]
    [InlineData("abc", -4594863902769663758L)]
    [InlineData("SURF-0x100004", 4258911377664598203L)]
    [InlineData("SURFACE-A", -5248365126419152322L)]
    [InlineData("FNST-0x100000", 6294069707949221965L)]
    [InlineData("\u00e9", 6047309291227476195L)]
    [InlineData("\u6f22", -6576571924611027090L)]
    [InlineData("\U0001f409", 8889289909682346436L)]
    [InlineData("A\u6f22\U0001f409", -581284729310515445L)]
    public void MatchesCpython312SipHash13WithHashSeedZero(string value, long expected)
    {
        Assert.Equal(expected, PythonSeedZeroStringHash.Compute(value));
    }
}

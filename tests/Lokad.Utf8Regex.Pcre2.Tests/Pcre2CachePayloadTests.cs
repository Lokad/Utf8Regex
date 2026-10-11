namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CachePayloadTests
{
    [Fact]
    public void LargeTemplatesRemainUsableWithoutBeingRetained()
    {
        var regex = new Utf8Pcre2Regex("x");
        var template = new string('a', 2 * 1024 * 1024);
        var result = regex.Replace("x"u8, template);
        Assert.Equal(template.Length, result.Length);
        Assert.All(result, value => Assert.Equal((byte)'a', value));
        Assert.Equal(0, regex.DebugReplacementCacheEntryCount);
    }
}

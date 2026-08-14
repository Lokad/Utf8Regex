using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementCacheTests
{
    [Fact]
    public void ReplacementPlanCacheIsBoundedUnderCallerControlledChurn()
    {
        var regex = new Utf8Pcre2Regex("foo");
        for (var i = 0; i < 100; i++)
        {
            _ = regex.Replace("foo"u8, $"replacement-{i}");
        }

        Assert.InRange(regex.DebugReplacementCacheEntryCount, 1, 16);
    }
}

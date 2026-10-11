namespace Lokad.Utf8Regex.Tests;

public sealed class ReplacementCachePayloadTests
{
    [Fact]
    public void LargeTemplatesRemainUsableWithoutBeingRetained()
    {
        var regex = new Utf8Regex("x");
        var template = new string('a', 2 * 1024 * 1024);
        var result = regex.Replace("x"u8, template);
        Assert.Equal(template.Length, result.Length);
        Assert.All(result, value => Assert.Equal((byte)'a', value));
        var field = typeof(Utf8Regex).GetField("_replacementCache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var cache = Assert.IsType<Internal.Replacement.Utf8ReplacementPlanCache>(field?.GetValue(regex));
        Assert.Equal(0, cache.Count);
    }
}

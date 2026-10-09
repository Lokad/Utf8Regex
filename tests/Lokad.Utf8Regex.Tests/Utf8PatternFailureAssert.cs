namespace Lokad.Utf8Regex.Tests;

internal static class Utf8PatternFailureAssert
{
    // Admission owns the pattern argument failure. .NET's parse exception is
    // an oracle rejection verdict, not the product's diagnostic contract.
    internal static ArgumentException Throws(Action construct)
    {
        var failure = Assert.Throws<ArgumentException>(construct);
        Assert.Equal("pattern", failure.ParamName);
        Assert.Contains("UTF-16 pattern offset", failure.Message, StringComparison.Ordinal);
        return failure;
    }
}

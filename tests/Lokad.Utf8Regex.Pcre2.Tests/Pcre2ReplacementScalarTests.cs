using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementScalarTests
{
    [Fact]
    public void PublishedSimpleTemplateStorageRequiresConstructionCapacity()
    {
        using var owner = Utf8ConstructionScope.Enter();
        Assert.True(Pcre2SimpleReplacementParser.TryParse("café😀$0z", Pcre2SubstitutionOptions.None, out _));
        Assert.True(owner.Budget.ReservedBytes > 0, "Published replacement arrays require live construction capacity.");
    }

    [Theory]
    [InlineData("(x)", "x", "$01", "x")]
    [InlineData("(x)", "x", "$00", "x")]
    [InlineData("(x)", "x", "${01}", "x")]
    public void LeadingZeroReferencesKeepTheirEntireNumericIdentifier(string pattern, string input, string replacement, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.Equal(expected, regex.ReplaceToString(Encoding.UTF8.GetBytes(input), replacement));
    }

    [Theory]
    [InlineData(@"\u𐐨𐐨", "𐐀𐐨")]
    [InlineData(@"\l𐐀𐐀", "𐐨𐐀")]
    [InlineData(@"\U𐐨a\E𐐨", "𐐀A𐐨")]
    [InlineData(@"\L𐐀A\E𐐀", "𐐨a𐐀")]
    [InlineData(@"\u\L$1", "𐐀𐐨")]
    [InlineData(@"\U\Q𐐨$1\E", "𐐀$1")]
    [InlineData(@"\u😀𐐨", "😀𐐨")]
    [InlineData(@"\U${1:+𐐨:𐐀}", "𐐀")]
    public void ExtendedCaseForcingConsumesOneCompleteScalar(string replacement, string expected)
    {
        var regex = new Utf8Pcre2Regex("(𐐨𐐀)");
        var input = "𐐨𐐀"u8;
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace(input, replacement, 0, Pcre2SubstitutionOptions.Extended));
        Assert.Equal(expected, regex.ReplaceToString(input, replacement, substitutionOptions: Pcre2SubstitutionOptions.Extended));
    }
}

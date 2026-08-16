using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class ExactUtf8LiteralIsMatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactUtf8LiteralIsMatchCoversPositionsMissesAndScalarWidths(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);

        AssertParity("café", "café then trailing text", options);
        AssertParity("café", "prefix then café then suffix", options);
        AssertParity("café", "prefix without the literal", options);
        AssertParity("😀", "prefix 😀 suffix", options);
        AssertParity("😀", "prefix without the scalar", options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactUtf8LiteralIsMatchStillRejectsMalformedBytesAroundACandidate(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex("café", options);
        var literal = Encoding.UTF8.GetBytes("café");
        byte[] malformedPrefix = [0xFF, .. literal];
        byte[] malformedSuffix = [.. literal, 0xFF];

        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformedPrefix));
        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformedSuffix));
        Assert.Throws<ArgumentException>(() => regex.Match(malformedPrefix));
        Assert.Throws<ArgumentException>(() => regex.Match(malformedSuffix));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequirementBearingExactUtf8LiteralsKeepRegexParity(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);

        AssertParity(@"\bcafé\b", "xcafé café", options);
        AssertParity(@"\bcafé\b", "xcaféy", options);
        AssertParity("café(?= noir)", "café gris café noir", options);
        AssertParity("café(?= noir)", "café gris", options);
    }

    [Fact]
    public void FiniteTimeoutAndRightToLeftExactUtf8LiteralsKeepRegexParity()
    {
        const string input = "café first, café last";
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var timeout = TimeSpan.FromSeconds(1);
        var finite = new Utf8Regex("café", RegexOptions.CultureInvariant, timeout);
        var rightToLeftOptions = RegexOptions.CultureInvariant | RegexOptions.RightToLeft;
        var rightToLeft = new Utf8Regex("café", rightToLeftOptions);

        var finiteOracle = new Regex("café", RegexOptions.CultureInvariant, timeout);
        var rightToLeftOracle = new Regex("café", rightToLeftOptions);

        Assert.Equal(finiteOracle.IsMatch(input), finite.IsMatch(inputBytes));
        AssertMatchParity(finiteOracle, finite, input, inputBytes);
        Assert.Equal(rightToLeftOracle.IsMatch(input), rightToLeft.IsMatch(inputBytes));
        AssertMatchParity(rightToLeftOracle, rightToLeft, input, inputBytes);
    }

    private static void AssertParity(string pattern, string input, RegexOptions options)
    {
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options);
        var inputBytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(oracle.IsMatch(input), regex.IsMatch(inputBytes));
        AssertMatchParity(oracle, regex, input, inputBytes);
    }

    private static void AssertMatchParity(Regex oracle, Utf8Regex regex, string input, byte[] inputBytes)
    {
        var expected = oracle.Match(input);
        var actual = regex.Match(inputBytes);

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        if (!expected.Success)
        {
            return;
        }

        Assert.True(actual.IsByteAligned);
        Assert.Equal(Encoding.UTF8.GetByteCount(input.AsSpan(0, expected.Index)), actual.IndexInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(input.AsSpan(expected.Index, expected.Length)), actual.LengthInBytes);
    }
}

using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class InvariantIgnoreCaseLiteralSemanticTests
{
    private const string Subject = "KILO cat";

    [Theory]
    [InlineData("kilo", false)]
    [InlineData("kilo", true)]
    [InlineData("KILO", false)]
    [InlineData("\\x6Bilo", false)]
    [InlineData("kilo|cat|dog", false)]
    [InlineData("kilo|cat|dog", true)]
    [InlineData("\\x6Bilo|cat|dog", false)]
    public void LiteralRoutesHonorKelvinSignCaseEquivalence(string pattern, bool compiled)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        var expected = new Regex(pattern, options);
        var actual = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(Subject);

        Assert.Equal(expected.IsMatch(Subject), actual.IsMatch(input));
        Assert.Equal(expected.Count(Subject), actual.Count(input));

        var expectedMatch = expected.Match(Subject);
        var actualMatch = actual.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        Assert.Equal(Encoding.UTF8.GetByteCount(Subject.AsSpan(0, expectedMatch.Index)), actualMatch.IndexInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(Subject.AsSpan(expectedMatch.Index, expectedMatch.Length)), actualMatch.LengthInBytes);

        var expectedRanges = expected.Matches(Subject)
            .Select(static match => (match.Index, match.Length))
            .ToArray();
        var actualRanges = new List<(int Index, int Length)>();
        foreach (var match in actual.EnumerateMatches(input))
        {
            actualRanges.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        Assert.Equal(expectedRanges, actualRanges);
    }

    [Theory]
    [InlineData("kilo", false)]
    [InlineData("kilo", true)]
    [InlineData("kilo|cat|dog", false)]
    [InlineData("kilo|cat|dog", true)]
    public void LiteralOutputHonorsKelvinSignCaseEquivalence(string pattern, bool compiled)
    {
        const string replacement = "X";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        var expectedRegex = new Regex(pattern, options);
        var expected = expectedRegex.Replace(Subject, replacement);
        var actual = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(Subject);

        Assert.Equal(expected, Encoding.UTF8.GetString(actual.Replace(input, replacement)));
        Assert.Equal(expected, Encoding.UTF8.GetString(actual.Replace(input, "X"u8)));
        Assert.Equal(expected, actual.ReplaceToString(input, replacement));

        Span<byte> destination = stackalloc byte[32];
        var status = actual.TryReplace(input, replacement, destination, out var bytesWritten);
        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination[..bytesWritten]));

        status = actual.TryReplace(input, "X"u8, destination, out bytesWritten);
        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination[..bytesWritten]));

        var actualSplits = new List<string>();
        foreach (var split in actual.EnumerateSplits(input))
        {
            actualSplits.Add(split.GetValueString());
        }

        Assert.Equal(expectedRegex.Split(Subject), actualSplits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KelvinFallbackStillRejectsMalformedUtf8(bool compiled)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        var regex = new Utf8Regex("kilo|cat|dog", options);
        var malformed = new byte[] { 0xE2, 0x84, 0xAA, 0xFF };

        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        Assert.Throws<ArgumentException>(() => regex.Match(malformed));
        Assert.Throws<ArgumentException>(() => ConsumeMatches(regex, malformed));
        Assert.Throws<ArgumentException>(() => regex.Replace(malformed, "X"));
    }

    private static void ConsumeMatches(Utf8Regex regex, byte[] input)
    {
        foreach (var _ in regex.EnumerateMatches(input))
        {
        }
    }
}

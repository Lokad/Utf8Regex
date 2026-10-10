using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexEnumerateSplitsDiffTests
{
    [Theory]
    [InlineData(",", "a,b,,c", int.MaxValue, RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    [InlineData(":", "a:b:c:d", 3, RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    [InlineData(",", "a,b,c,d", 3, RegexOptions.CultureInvariant | RegexOptions.RightToLeft | RegexOptions.ExplicitCapture)]
    public void EnumerateSplitsMatchesRuntimeForMirroredDotNetBatch(string pattern, string input, int count, RegexOptions options)
    {
        // Mirrored and adapted from (research checkout external/runtime @ 9ffface2f3f):
        // external/runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.EnumerateSplits.Tests.cs
        AssertEnumerateSplitsParity(pattern, input, count, options);
    }

    [Theory]
    [InlineData("(a)", "baac", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData(",", "a,b,,c", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData(":", "a:b:c:d", 3, RegexOptions.CultureInvariant)]
    [InlineData(".", "😀", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "cat horse fish horse", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData(@"\d{2,4}", "1 12 123 1234", int.MaxValue, RegexOptions.CultureInvariant)]
    public void EnumerateSplitsMatchesScalarContract(string pattern, string input, int count, RegexOptions options)
    {
        var subject = new Utf8ScalarDiffSubject(input);
        var oracle = subject.Comparator(pattern, options);
        var expected = new List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(subject.Projected, count))
        {
            var (index, length) = range.GetOffsetAndLength(subject.Projected.Length);
            var (start, units) = subject.Units(index, length);
            expected.Add((subject.Value(index, length), start, units));
        }
        var regex = new Utf8Regex(pattern, options);
        Assert.Equal(expected, EnumerateUtf8Splits(regex, input, count));
        foreach (var split in regex.EnumerateSplits(Encoding.UTF8.GetBytes(input), count))
            Assert.True(split.IsByteAligned);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void StaticEnumerateSplitsMatchesInstanceBehavior()
    {
        var instance = EnumerateUtf8Splits(new Utf8Regex("(a)", RegexOptions.CultureInvariant), "baac", int.MaxValue);
        var actual = new List<(string Value, int Index, int Length)>();

        foreach (var split in Utf8Regex.EnumerateSplits("baac"u8, "(a)", int.MaxValue, RegexOptions.CultureInvariant))
        {
            actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(instance, actual);
    }

    [Fact]
    public void EnumerateSplitsMatchesRuntimeForNativeCountLimitedSimplePattern()
    {
        const string input = "a,b,c,d";
        const string pattern = ",";
        const int count = 3;
        var utf8Regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        var expected = EnumerateRuntimeSplits(input, pattern, count, RegexOptions.CultureInvariant);
        var actual = EnumerateUtf8Splits(utf8Regex, input, count);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateSplitsMatchesRuntimeForNativeUtf8Literal()
    {
        const string input = "αcaféβcaféγ";
        const string pattern = "café";
        var utf8Regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        var expected = EnumerateRuntimeSplits(input, pattern, int.MaxValue, RegexOptions.CultureInvariant);
        var actual = EnumerateUtf8Splits(utf8Regex, input, int.MaxValue);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateSplitsMatchesRuntimeForNativeUtf8LiteralAlternation()
    {
        const string input = "αcaféβniñoγrésuméδ";
        const string pattern = "café|niño|résumé";
        var utf8Regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        var expected = EnumerateRuntimeSplits(input, pattern, int.MaxValue, RegexOptions.CultureInvariant);
        var actual = EnumerateUtf8Splits(utf8Regex, input, int.MaxValue);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateSplitsMatchesRuntimeForBoundaryWrappedUtf8LiteralAlternation()
    {
        const string input = "café niño caféx xniño résumé";
        const string pattern = @"\b(?:café|niño|résumé)\b";
        var utf8Regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        var expected = EnumerateRuntimeSplits(input, pattern, int.MaxValue, RegexOptions.CultureInvariant);
        var actual = EnumerateUtf8Splits(utf8Regex, input, int.MaxValue);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateSplitsMatchesRuntimeForAsciiLoggingInvocationFamily()
    {
        const string input = "LogError(\"bad\"); LogDebug(\"skip\"); LogInformation (\"ok\");";
        const string pattern = @"\b(?:LogError|LogWarning|LogInformation)\s*\(";
        var utf8Regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        var expected = EnumerateRuntimeSplits(input, pattern, int.MaxValue, RegexOptions.CultureInvariant);
        var actual = EnumerateUtf8Splits(utf8Regex, input, int.MaxValue);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateSplitsMatchesRuntimeForCapturePattern()
    {
        const string input = "baac";
        const string pattern = "(a)";
        var utf8Regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        var expected = EnumerateRuntimeSplits(input, pattern, int.MaxValue, RegexOptions.CultureInvariant);
        var actual = EnumerateUtf8Splits(utf8Regex, input, int.MaxValue);

        Assert.Equal(new[] { "b", string.Empty, "c" }, actual.Select(x => x.Value));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateSplitsCountMatchesRuntimeExactly()
    {
        const string input = "a:b:c:d";
        const string pattern = ":";
        const int count = 3;
        var utf8Regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        var expected = EnumerateRuntimeSplits(input, pattern, count, RegexOptions.CultureInvariant);
        var actual = EnumerateUtf8Splits(utf8Regex, input, count);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateSplitsMatchesRuntimeForRightToLeftOrdering()
    {
        const string input = "a,b,c,d";
        const string pattern = ",";
        const int count = 3;
        const RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = EnumerateRuntimeSplits(input, pattern, count, options);
        var actual = EnumerateUtf8Splits(utf8Regex, input, count);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("a|ab|abc", "abcababa", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData("a|ab|abc", "abcababa", 3, RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "CAT horse Cat HORSE cat", int.MaxValue, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "CAT horse Cat HORSE cat", 3, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "cathorsecat", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData("x|y|z", "", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData("x|y|z", "no match here", 2, RegexOptions.CultureInvariant)]
    [InlineData("(?:cat|horse)", "cat horse cathorse", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData("(?:cat|horse)", "cat horse cathorse", 2, RegexOptions.CultureInvariant)]
    [InlineData("(?:cat|horse)(?=horse)", "cathorsecathorse", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData("(?:cat|horse)(?=horse)", "cathorsecathorse", 2, RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "cat horse fish horse", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "horsecat", 2, RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "cathorse", 3, RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    [InlineData("(?<w>cat)|(?<v>horse)", "cat horse fish", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData("(cat)|horse", "horsecat", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData("(a)|(ab)", "xab", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "CAT horse", int.MaxValue, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("(caf\u00E9)|(ni\u00F1o)", "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", int.MaxValue, RegexOptions.None)]
    [InlineData("\\b(cat|horse)\\b", "acat horsecatx cathorse", int.MaxValue, RegexOptions.CultureInvariant)]
    [InlineData("\\b(cat|horse)\\b", "acat horsecatx cathorse", 2, RegexOptions.CultureInvariant)]
    [InlineData("\\b(caf\u00E9|ni\u00F1o)\\b", "caf\u00E9 xcaf\u00E9 ni\u00F1o", int.MaxValue, RegexOptions.CultureInvariant)]
    public void EnumerateSplitsMatchesRuntimeForOwnedLiteralFamily(string pattern, string input, int count, RegexOptions options)
    {
        var utf8Regex = new Utf8Regex(pattern, options);
        var expected = EnumerateRuntimeSplits(input, pattern, count, options);
        var actual = EnumerateUtf8Splits(utf8Regex, input, count);

        Assert.Equal(expected, actual);
        Assert.True(utf8Regex.Inspection.DebugCanUseNativeSplit(Encoding.UTF8.GetBytes(input)));
    }

    private static List<(string Value, int Index, int Length)> EnumerateRuntimeSplits(
        string input,
        string pattern,
        int count,
        RegexOptions options)
    {
        var regex = new Regex(pattern, options);
        var values = new List<(string Value, int Index, int Length)>();

        foreach (var range in regex.EnumerateSplits(input, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            values.Add((input[start..end], start, end - start));
        }

        return values;
    }

    private static List<(string Value, int Index, int Length)> EnumerateUtf8Splits(Utf8Regex utf8Regex, string input, int count)
    {
        var values = new List<(string Value, int Index, int Length)>();

        foreach (var split in utf8Regex.EnumerateSplits(Encoding.UTF8.GetBytes(input), count))
        {
            values.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        return values;
    }

    private static void AssertEnumerateSplitsParity(string pattern, string input, int count, RegexOptions options)
    {
        var expected = EnumerateRuntimeSplits(input, pattern, count, options);
        var expectedCompiled = EnumerateRuntimeSplits(input, pattern, count, options | RegexOptions.Compiled);
        var actual = EnumerateUtf8Splits(new Utf8Regex(pattern, options), input, count);
        var actualCompiled = EnumerateUtf8Splits(new Utf8Regex(pattern, options | RegexOptions.Compiled), input, count);

        Assert.Equal(expected, expectedCompiled);
        Assert.Equal(expected, actual);
        Assert.Equal(expected, actualCompiled);
    }
}

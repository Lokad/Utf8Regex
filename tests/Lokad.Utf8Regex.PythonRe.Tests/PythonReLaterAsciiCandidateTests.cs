using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLaterAsciiCandidateTests
{
    [Theory]
    [InlineData(63, 2047)]
    [InlineData(63, 2048)]
    [InlineData(63, 2049)]
    [InlineData(1025, 2048)]
    public void RequiredLaterClassKeepsEarliestStartsCapturesAndAnchoredOperations(int width, int padding)
    {
        var prefix = new string('a', width);
        using var regex = new Utf8PythonRegex(prefix + "([0-9]+)");
        var text = "π😀" + new string('a', padding + width) + "3!" + prefix + "45";
        var input = Encoding.UTF8.GetBytes(text);
        var start = 6 + padding;
        var second = start + width + 2;
        var matches = regex.FindIterDetailed(input);
        Assert.Equal(2, matches.Length);
        Assert.Equal(start, matches[0].Value.StartOffsetInBytes);
        Assert.Equal(start - 3, matches[0].Value.StartOffsetInUtf16);
        Assert.Equal("3", matches[0].Groups[1].ValueText);
        Assert.Equal(second, matches[1].Value.StartOffsetInBytes);
        Assert.Equal("45", matches[1].Groups[1].ValueText);
        Assert.Equal(new[] { "3", "45" }, regex.FindAllToStrings(input).ScalarValues);
        Assert.Equal(second, regex.Search(input, start + 1).StartOffsetInBytes);
        Assert.False(regex.Match(input).Success);
        Assert.True(regex.Match(input, start).Success);
        Assert.True(regex.FullMatch(input.AsSpan(start, width + 1)).Success);
        _ = regex.FindAllToStrings("unrelated"u8);
        Assert.Equal("3", matches[0].Groups[1].ValueText);
    }

    [Fact]
    public void RewoundFalseCandidatesCannotStartInsideAScalar()
    {
        var prefix = new string('a', 63);
        var skipped = "é" + new string('a', 62) + "3!";
        using var regex = new Utf8PythonRegex(prefix + "([0-9]+)");
        var input = Encoding.UTF8.GetBytes(skipped + prefix + "7");
        Assert.Equal(Encoding.UTF8.GetByteCount(skipped), regex.Search(input).StartOffsetInBytes);
        Assert.Equal(new[] { "7" }, regex.FindAllToStrings(input).ScalarValues);
    }

    [Fact]
    public void OutputAndCursorConsumersKeepCapturesAndRetainedAliases()
    {
        var prefix = new string('a', 63);
        using var regex = new Utf8PythonRegex(prefix + "([0-9]+)");
        var skipped = "😀" + new string('a', 2049);
        var input = Encoding.UTF8.GetBytes(skipped + prefix + "12!" + prefix + "34");
        Assert.Equal(skipped + "<12>!<34>", regex.ReplaceToString(input, "<\\1>"));
        Assert.Equal(new[] { skipped, "12", "!", "34", "" }, regex.SplitToStrings(input));
        using var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        var retained = alias.Current;
        Assert.Equal("12", retained.Groups[1].ValueText);
        Assert.True(alias.MoveNext());
        Assert.Equal("34", cursor.Current.Groups[1].ValueText);
        Assert.False(cursor.MoveNext());
        alias.Dispose();
        Assert.Equal("12", retained.Groups[1].ValueText);
    }

    [Theory]
    [InlineData("([0-9]*)", "")]
    [InlineData("([0-9]+)?", "")]
    [InlineData("([0-9]+|Z+)", "ZZ")]
    [InlineData("([0-9]+|a+)", "aaa")]
    [InlineData("([^a]+)", "é")]
    [InlineData("(?=([0-9]+))[0-9]+", "34")]
    [InlineData("(?i:([a-z]+))", "Z")]
    public void OptionalBranchAssertionAndUnicodeSuffixesKeepTheOriginalResults(string suffix, string tail)
    {
        var prefix = new string('a', 63);
        var pattern = prefix + suffix;
        var text = "π" + new string('a', 65) + tail + "!";
        using var regex = new Utf8PythonRegex(pattern);
        var expected = new Regex(pattern, RegexOptions.CultureInvariant).Matches(text)
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(expected, regex.FindAllToStrings(Encoding.UTF8.GetBytes(text)).ScalarValues);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongFalsePrefixAdmitsTheSamePublicResultUnderFiniteWork(bool denseFalseLaterBytes)
    {
        using var regex = new Utf8PythonRegex(new string('a', 1025) + "([0-9]+)", resourcePolicy: new()
        { MaxWorkSteps = 2000, MaxDepth = 4096, MaxWorkingBytes = 16UL << 20, MaxResultBytes = 1UL << 20 });
        using var cancellation = new CancellationTokenSource();
        var text = denseFalseLaterBytes
            ? string.Concat(Enumerable.Repeat("x3", 32768)) + new string('a', 1025) + "3"
            : new string('a', 65536) + "3";
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(new[] { "3" }, regex.FindAllToStrings(input, 0, cancellation.Token).ScalarValues);
        Assert.Empty(regex.FindAllToStrings(input.AsSpan(0, input.Length - 1), 0, cancellation.Token).ScalarValues);
    }
}

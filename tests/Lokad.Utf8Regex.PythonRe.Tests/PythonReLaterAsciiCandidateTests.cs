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
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var matches = collectedRanges1.ToArray();
        Assert.Equal(2, matches.Length);
        Assert.Equal(start, matches[0].Value.StartOffsetInBytes);
        Assert.Equal("3", System.Text.Encoding.UTF8.GetString(matches[0].GetGroupMemory(1).Span));
        Assert.Equal(second, matches[1].Value.StartOffsetInBytes);
        Assert.Equal("45", System.Text.Encoding.UTF8.GetString(matches[1].GetGroupMemory(1).Span));
        Assert.Equal(new[] { "3", "45" }, PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input)).ScalarValues);
        Assert.Equal(second, regex.Match(input,start + 1,mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        Assert.False(regex.Match(input).Success);
        Assert.True(regex.Match(input,start).Success);
        Assert.True(regex.Match(input.AsSpan(start, width + 1),mode: Utf8PythonMatchMode.FullMatch).Success);
        _ = PythonReFindAllAssert.Decode(regex.FindAllToUtf8("unrelated"u8));
        Assert.Equal("3", System.Text.Encoding.UTF8.GetString(matches[0].GetGroupMemory(1).Span));
    }

    [Fact]
    public void RewoundFalseCandidatesCannotStartInsideAScalar()
    {
        var prefix = new string('a', 63);
        var skipped = "é" + new string('a', 62) + "3!";
        using var regex = new Utf8PythonRegex(prefix + "([0-9]+)");
        var input = Encoding.UTF8.GetBytes(skipped + prefix + "7");
        Assert.Equal(Encoding.UTF8.GetByteCount(skipped), regex.Match(input,mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        Assert.Equal(new[] { "7" }, PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input)).ScalarValues);
    }

    [Fact]
    public void OutputAndCursorConsumersKeepCapturesAndRetainedAliases()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var prefix = new string('a', 63);
        using var regex = new Utf8PythonRegex(prefix + "([0-9]+)");
        var skipped = "😀" + new string('a', 2049);
        var input = Encoding.UTF8.GetBytes(skipped + prefix + "12!" + prefix + "34");
        Assert.Equal(skipped + "<12>!<34>", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<\\1>")));
        Assert.Equal(new[] { skipped, "12", "!", "34", "" }, PythonReSplitAssert.Decode(input, regex.SplitRanges(input, 0, 0, default)));
        using var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        Assert.True(cursor.MoveNext(out cursorRange));
        var retained = cursorRange;
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        Assert.True(alias.MoveNext(out cursorRange));
        Assert.Equal("34", System.Text.Encoding.UTF8.GetString(cursorRange.GetGroupMemory(1).Span));
        Assert.False(cursor.MoveNext(out cursorRange));
        alias.Dispose();
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
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
        Assert.Equal(expected, PythonReFindAllAssert.Decode(regex.FindAllToUtf8(Encoding.UTF8.GetBytes(text))).ScalarValues);
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
        Assert.Equal(new[] { "3" }, PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, 0, cancellation.Token)).ScalarValues);
        Assert.Empty(PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input.AsSpan(0, input.Length - 1), 0, cancellation.Token)).ScalarValues);
    }
}

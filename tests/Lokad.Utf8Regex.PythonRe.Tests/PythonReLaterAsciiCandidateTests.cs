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
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(new[] { "3", "45" }, snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(second, regex.Match(input, start + 1, mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        Assert.False(regex.Match(input).Success);
        Assert.True(regex.Match(input, start).Success);
        Assert.True(regex.Match(input.AsSpan(start, width + 1), mode: Utf8PythonMatchMode.FullMatch).Success);
        ReadOnlyMemory<byte> snapshots2Input = ("unrelated"u8).ToArray();
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        _ = snapshots2;
        Assert.Equal("3", System.Text.Encoding.UTF8.GetString(matches[0].GetGroupMemory(1).Span));
    }

    [Fact]
    public void RewoundFalseCandidatesCannotStartInsideAScalar()
    {
        var prefix = new string('a', 63);
        var skipped = "é" + new string('a', 62) + "3!";
        using var regex = new Utf8PythonRegex(prefix + "([0-9]+)");
        var input = Encoding.UTF8.GetBytes(skipped + prefix + "7");
        Assert.Equal(Encoding.UTF8.GetByteCount(skipped), regex.Match(input, mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        ReadOnlyMemory<byte> snapshots3Input = input;
        var snapshots3 = new List<Utf8PythonRangeMatch>();
        using (var snapshots3Cursor = regex.CreateMatchCursor(snapshots3Input, 0, default))
        {
            while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                snapshots3.Add(snapshots3Match);
        }
        Assert.Equal(new[] { "7" }, snapshots3.Select(captureSnapshot2 => System.Text.Encoding.UTF8.GetString(captureSnapshot2.GetGroupMemory(captureSnapshot2.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }

    [Fact]
    public void OutputAndCursorConsumersKeepCapturesAndRetainedAliases()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var prefix = new string('a', 63);
        using var regex = new Utf8PythonRegex(prefix + "([0-9]+)");
        var skipped = "😀" + new string('a', 2049);
        var input = Encoding.UTF8.GetBytes(skipped + prefix + "12!" + prefix + "34");
        Assert.Equal(skipped + "<12>!<34>", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<\\1>"u8)));
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
        ReadOnlyMemory<byte> snapshots4Input = Encoding.UTF8.GetBytes(text);
        var snapshots4 = new List<Utf8PythonRangeMatch>();
        using (var snapshots4Cursor = regex.CreateMatchCursor(snapshots4Input, 0, default))
        {
            while (snapshots4Cursor.MoveNext(out var snapshots4Match))
                snapshots4.Add(snapshots4Match);
        }
        Assert.Equal(expected, snapshots4.Select(captureSnapshot3 => System.Text.Encoding.UTF8.GetString(captureSnapshot3.GetGroupMemory(captureSnapshot3.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongFalsePrefixAdmitsTheSamePublicResultUnderFiniteWork(bool denseFalseLaterBytes)
    {
        using var regex = new Utf8PythonRegex(new string('a', 1025) + "([0-9]+)", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxDepth = 4096,
                MaxWorkingBytes = 16UL << 20,
                MaxResultBytes = 1UL << 20
            }
        });
        using var cancellation = new CancellationTokenSource();
        var text = denseFalseLaterBytes
            ? string.Concat(Enumerable.Repeat("x3", 32768)) + new string('a', 1025) + "3"
            : new string('a', 65536) + "3";
        var input = Encoding.UTF8.GetBytes(text);
        ReadOnlyMemory<byte> snapshots5Input = input;
        var snapshots5 = new List<Utf8PythonRangeMatch>();
        using (var snapshots5Cursor = regex.CreateMatchCursor(snapshots5Input, 0, cancellation.Token))
        {
            while (snapshots5Cursor.MoveNext(out var snapshots5Match))
                snapshots5.Add(snapshots5Match);
        }
        Assert.Equal(new[] { "3" }, snapshots5.Select(captureSnapshot4 => System.Text.Encoding.UTF8.GetString(captureSnapshot4.GetGroupMemory(captureSnapshot4.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots6Input = (input.AsSpan(0, input.Length - 1)).ToArray();
        var snapshots6 = new List<Utf8PythonRangeMatch>();
        using (var snapshots6Cursor = regex.CreateMatchCursor(snapshots6Input, 0, cancellation.Token))
        {
            while (snapshots6Cursor.MoveNext(out var snapshots6Match))
                snapshots6.Add(snapshots6Match);
        }
        Assert.Empty(snapshots6.Select(captureSnapshot5 => System.Text.Encoding.UTF8.GetString(captureSnapshot5.GetGroupMemory(captureSnapshot5.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }
}

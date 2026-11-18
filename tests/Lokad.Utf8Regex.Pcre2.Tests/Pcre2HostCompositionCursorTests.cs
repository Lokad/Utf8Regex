using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2HostCompositionCursorTests
{
    [Theory]
    [InlineData("x", "αx😀xβ", "αR😀Rβ")]
    [InlineData("[x]", "αx😀xβ", "αR😀Rβ")]
    [InlineData("a+", "éaaa😀aaβ", "éR😀Rβ")]
    [InlineData("a|ab", "éab😀aβ", "éRb😀Rβ")]
    [InlineData("", "😀", "R😀R")]
    [InlineData(".", "é😀", "RR")]
    public void HostCompositionUsesScalarRangesAndRetainsPublishedAliases(string pattern, string text, string expected)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1_000_000,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = Encoding.UTF8.GetBytes(text);
        using var cursor = regex.CreateMatchCursor(input, cancellationToken: source.Token);
        var output = new StringBuilder();
        var observations = new List<(int Start, int End, string Text)>();
        var first = default(Utf8Pcre2RangeMatch);
        var position = 0;
        while (cursor.MoveNext(out var match))
        {
            if (!first.Success) first = match;
            var value = match.Value;
            observations.Add((value.StartOffsetInBytes, value.EndOffsetInBytes,
                Encoding.UTF8.GetString(match.GetValueMemory().Span)));
            output.Append(Encoding.UTF8.GetString(input.AsSpan(position, value.StartOffsetInBytes - position)));
            output.Append('R');
            position = value.EndOffsetInBytes;
        }
        output.Append(Encoding.UTF8.GetString(input.AsSpan(position)));
        cursor.Dispose();
        Assert.Equal(expected, output.ToString());
        Assert.Equal(observations[0].Text, Encoding.UTF8.GetString(first.GetValueMemory().Span));
        if (pattern == ".") Assert.Equal([(0, 2, "é"), (2, 6, "😀")], observations);
        if (pattern.Length == 0) Assert.Equal([(0, 0, ""), (4, 4, "")], observations);
    }

    public static IEnumerable<object[]> AllOptions()
    {
        for (var flags = 0; flags < 64; flags++) yield return [(Pcre2MatchOptions)flags];
    }

    [Theory]
    [MemberData(nameof(AllOptions))]
    public void OptionCombinationsPreserveActualCapturedYields(Pcre2MatchOptions options)
    {
        var regex = new Utf8Pcre2Regex("(é)(😀)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1_000_000,
                MaxWorkingBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = "qé😀é😀"u8.ToArray();
        using var cursor = regex.CreateMatchCursor(input, 1, options);
        var output = new StringBuilder();
        var captures = new List<(int, int, string)>();
        var position = 0;
        while (cursor.MoveNext(out var match))
        {
            var value = match.Value;
            var group = match.Groups[2];
            var text = Encoding.UTF8.GetString(match.GetGroupMemory(2).Span);
            captures.Add((group.StartOffsetInBytes, group.EndOffsetInBytes, text));
            output.Append(Encoding.UTF8.GetString(input.AsSpan(position, value.StartOffsetInBytes - position)));
            output.Append('!').Append(text);
            position = value.EndOffsetInBytes;
        }
        output.Append(Encoding.UTF8.GetString(input.AsSpan(position)));
        var endAnchored = (options & Pcre2MatchOptions.EndAnchored) != 0;
        var anchored = (options & Pcre2MatchOptions.Anchored) != 0;
        Assert.Equal(endAnchored ? anchored ? "qé😀é😀" : "qé😀!😀" : "q!😀!😀", output.ToString());
        Assert.Equal(endAnchored ? anchored ? [] : [(9, 13, "😀")] : [(3, 7, "😀"), (9, 13, "😀")], captures);
    }

    [Fact]
    public void OptionalNamedGroupsRemainAvailableToHostComputation()
    {
        var input = "foo fooBAR x"u8.ToArray();
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");
        using var cursor = regex.CreateMatchCursor(input);
        var values = new List<string>();
        while (cursor.MoveNext(out var match))
        {
            var named = match.TryGetFirstSetGroup("Bar", out var range)
                ? Encoding.UTF8.GetString(input.AsSpan(range.StartOffsetInBytes, range.EndOffsetInBytes - range.StartOffsetInBytes))
                : "none";
            values.Add($"{Encoding.UTF8.GetString(match.GetValueMemory().Span)}|{named}");
        }
        Assert.Equal(["foo|none", "fooBAR|BAR"], values);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionOnNextAdvancePreservesPublishedCaptures(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(é)(😀)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1_000_000,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        using var cursor = regex.CreateMatchCursor("é😀é😀"u8.ToArray(), matchOptions: Pcre2MatchOptions.Anchored,
            cancellationToken: source.Token);
        Assert.True(cursor.MoveNext(out var retained));
        if (cancel) source.Cancel(); else ticks = 25;
        if (cancel)
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _)).CancellationToken);
        else
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() => cursor.MoveNext(out _)).ErrorKind);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
        cursor.Dispose();
        Assert.Equal("😀"u8.ToArray(), retained.GetGroupMemory(2).ToArray());
    }
}

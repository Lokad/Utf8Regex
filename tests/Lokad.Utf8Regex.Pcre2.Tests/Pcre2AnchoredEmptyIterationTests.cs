using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2AnchoredEmptyIterationTests
{
    [Theory]
    [InlineData("(a*)", "bbb", 0, 1, "0:0", "Xbbb")]
    [InlineData("(a*)", "bbb", 1, 1, "1:1", "bXbb")]
    [InlineData("(a)?", "ab", 0, 2, "0:1|1:1", "XXb")]
    [InlineData("(?:|a)", "ab", 0, 3, "0:0|0:1|1:1", "XXXb")]
    [InlineData("a*?", "ab", 0, 3, "0:0|0:1|1:1", "XXXb")]
    [InlineData("(?:a?|b)", "b", 0, 3, "0:0|0:1|1:1", "XXX")]
    [InlineData("(a*)", "😀a", 0, 1, "0:0", "X😀a")]
    [InlineData("(a*)", "😀a", 4, 2, "4:5|5:5", "😀XX")]
    [InlineData("(?m)^", "a\nb", 0, 1, "0:0", "Xa\nb")]
    [InlineData(@"a\K", "aaa", 0, 3, "1:1|2:2|3:3", "aXaXaX")]
    public void FailedAnchoredRetryStopsButSuccessfulRetryRemainsPossible(
        string pattern, string text, int start, int count, string expectedRanges, string replacement)
    {
        // These ranges and replacements are native PCRE2 10.47 witnesses.
        foreach (var (compile, options) in new[]
        {
            (Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None),
            (Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored),
            (Pcre2CompileOptions.Anchored, Pcre2MatchOptions.Anchored),
        })
        foreach (var finite in new[] { false, true })
        {
            var regex = Create(pattern, compile, finite);
            var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
            var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            using var token = new CancellationTokenSource();
            Assert.Equal(count, regex.Count(input, start, options));
            Assert.Equal(count, regex.Count(input, start, options, token.Token));
            var ranges = new List<string>();
            var cursor = regex.EnumerateMatches(input, start, options);
            while (cursor.MoveNext())
                ranges.Add($"{cursor.Current.StartOffsetInBytes}:{cursor.Current.EndOffsetInBytes}");
            Assert.Equal(expectedRanges, string.Join("|", ranges));
            Assert.Equal(replacement, regex.ReplaceToString(input, "X", start,
                Pcre2SubstitutionOptions.None, options, token.Token));
            Assert.Equal(replacement, regex.ReplaceToString(input, 0,
                static (in Utf8Pcre2MatchContext _, ref int state) => { state++; return "X"; },
                start, options, token.Token));
        }
    }

    [Fact]
    public void AnchoringKeepsTheFrozenOperationSpecificResetDisposition()
    {
        var regex = Create(@"(?:a\K)*", Pcre2CompileOptions.None, true);
        Assert.Equal(1, regex.Count("ab"u8, 0, Pcre2MatchOptions.Anchored));
        // SPEC-PCRE2 deliberately preserves reset-empty results for replacement
        // that ordinary Count/enumeration suppresses. Anchoring still stops bumping.
        Assert.Equal("aXXb", regex.ReplaceToString("ab"u8, "X", 0,
            Pcre2SubstitutionOptions.None, Pcre2MatchOptions.Anchored));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InterruptedAnchoredReplacementStillReleasesScratchAndRetainsResults(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(a*)", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, TimeSpan.FromMilliseconds(20));
        var retained = regex.MatchDetailed("aaa"u8);
        Utf8SliceByteOutputScope? scope = null;
        Exception? failure = null;
        try
        {
            _ = regex.ReplaceToString("bbb"u8, 0,
                (in Utf8Pcre2MatchContext _, ref int state) =>
                {
                    scope = Utf8SliceByteOutputScope.Current;
                    if (cancel) token.Cancel(); else ticks = 25;
                    return "X";
                }, 0, Pcre2MatchOptions.Anchored, token.Token);
        }
        catch (Exception error) { failure = error; }
        if (cancel)
            Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.NotNull(scope);
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        Assert.Equal("aaa", retained.GetGroup(1).GetValueString());
    }

    private static Utf8Pcre2Regex Create(string pattern, Pcre2CompileOptions compile, bool finite) =>
        new(pattern, compile, default, finite ? new() { MatchLimit = 1000000, DepthLimit = 256,
            HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 } : default,
            Timeout.InfiniteTimeSpan);
}

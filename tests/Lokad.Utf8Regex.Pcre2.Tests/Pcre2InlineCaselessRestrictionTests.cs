using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2InlineCaselessRestrictionTests
{
    // Native PCRE2 10.47: r blocks ASCII/non-ASCII fold partners, while
    // preserving ordinary case folding and each class's explicitly included set.
    [Theory]
    [InlineData(@"(?ir)k", "K", true)]
    [InlineData(@"(?ir)k", "K", false)]
    [InlineData(@"(?ir)K", "K", false)]
    [InlineData(@"(?ir)K", "K", true)]
    [InlineData(@"(?ir)s", "S", true)]
    [InlineData(@"(?ir)s", "ſ", false)]
    [InlineData(@"(?ir)ſ", "s", false)]
    [InlineData(@"(?ir)[a-z]", "K", true)]
    [InlineData(@"(?ir)[a-z]", "K", false)]
    [InlineData(@"(?ir)[^a-z]", "K", true)]
    [InlineData(@"(?ir)[^a-z]", "K", false)]
    [InlineData(@"(?ir)[kK]", "K", true)]
    [InlineData(@"(?ir)[kK]", "K", true)]
    [InlineData(@"(?ir)[^kK]", "K", false)]
    [InlineData(@"(?ir)é", "É", true)]
    [InlineData(@"(?ir)\x{10400}", "𐐨", true)]
    [InlineData(@"(?r)k", "K", false)]
    [InlineData(@"(?i)(?r:k)s", "Kſ", true)]
    [InlineData(@"(?ir)k(?^i)s", "Kſ", true)]
    [InlineData(@"(?i)(?:a(?r)k|s)", "ſ", false)]
    [InlineData(@"(?ir:[k])+K", "KKK", true)]
    [InlineData(@"(?i:[k])+K", "KK", true)]
    [InlineData(@"(?i)(k)(?r)\1", "kK", false)]
    [InlineData(@"(?i)(k)(?r)\1", "kK", true)]
    [InlineData(@"(?ir)(k)(?-r)\1", "kK", true)]
    [InlineData(@"(?i)(?r)(?(DEFINE)(?<g>k))(?-r)(?&g)", "K", false)]
    [InlineData(@"(?(DEFINE)(?<g>(?i:k)))(?r)(?&g)", "K", true)]
    [InlineData(@"(?ir)(?-ri)k", "K", false)]
    [InlineData(@"(?ir)(k)(?r-i:\1)", "kK", false)]
    [InlineData(@"(?J)(?i)(?:(?<x>k)|(?<x>s))(?r:\k<x>)", "sſ", false)]
    [InlineData(@"(?J)(?i)(?:(?<x>k)|(?<x>s))(?r:\k<x>)", "sS", true)]
    public void InlineRestrictionPreservesScalarMatchingScopesAndReferences(
        string pattern, string text, bool expected)
    {
        var bytes = Encoding.UTF8.GetBytes("padding" + text + "padding");
        var input = bytes.AsSpan(7, Encoding.UTF8.GetByteCount(text));
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { Pcre2CompileOptions.None, Pcre2CompileOptions.Ucp })
        foreach (var limits in new[] { default(Utf8Pcre2ExecutionLimits), new Utf8Pcre2ExecutionLimits
        {
            MatchLimit = 1000000, DepthLimit = 256,
            HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20
        } })
        {
            var regex = new Utf8Pcre2Regex(pattern, options, default, limits, Timeout.InfiniteTimeSpan);
            Assert.Equal(expected, regex.IsMatch(input, 0, Pcre2MatchOptions.None, token.Token));
            Assert.Equal(expected ? 1 : 0, regex.Count(input, 0, Pcre2MatchOptions.None, token.Token));
            var match = regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, token.Token);
            Assert.Equal(expected, match.Success);
            if (expected)
                Assert.Equal((0, input.Length), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        }
    }

    [Theory]
    [InlineData(@"(?i)[[:upper:]]", "k", false, true)]
    [InlineData(@"(?ir)[[:upper:]]", "k", false, true)]
    [InlineData(@"(?ir)[[:lower:]]", "K", false, true)]
    [InlineData(@"(?ir)[[:upper:]]", "K", true, false)]
    [InlineData(@"(?ir)[[:lower:]]", "ſ", true, false)]
    [InlineData(@"(?ir)\p{Lu}", "é", true, true)]
    public void PosixCategoriesRetainTheNativeUcpDistinction(
        string pattern, string text, bool unicodeExpected, bool asciiExpected)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(unicodeExpected, new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Ucp).IsMatch(bytes));
        Assert.Equal(asciiExpected, new Utf8Pcre2Regex(pattern).IsMatch(bytes));
    }

    [Fact]
    public void EmptyDirectiveQuotesAndExtendedCommentsKeepTheirMeaning()
    {
        var cursor = new Utf8Pcre2Regex("(?r)").EnumerateMatches("😀"u8);
        Assert.True(cursor.MoveNext()); Assert.Equal(0, cursor.Current.StartOffsetInBytes);
        Assert.True(cursor.MoveNext()); Assert.Equal(4, cursor.Current.StartOffsetInBytes);
        Assert.False(cursor.MoveNext());
        Assert.True(new Utf8Pcre2Regex(@"\Q(?ir)\E").IsMatch("(?ir)"u8));
        Assert.True(new Utf8Pcre2Regex("(?xir) k # (?C1)\n").IsMatch("K"u8));
        Assert.False(new Utf8Pcre2Regex("(?xir) k # (?C1)\n").IsMatch("K"u8));
    }

    [Fact]
    public void RestrictionKeepsAllFourFiniteDenials()
    {
        const string pattern = @"(?ir)(?<x>(?:k|s)+)\k<x>";
        foreach (var kind in new[] { 0, 1, 2, 3 })
        {
            var limits = kind switch
            {
                0 => new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
                1 => new Utf8Pcre2ExecutionLimits { DepthLimit = 1 },
                2 => new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 },
                _ => new Utf8Pcre2ExecutionLimits { ResultLimitInBytes = 1 },
            };
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
                limits, Timeout.InfiniteTimeSpan);
            var error = Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed("ksKS"u8));
            Assert.Equal(kind switch { 0 => Pcre2ErrorKind.MatchLimit, 1 => Pcre2ErrorKind.DepthLimit,
                2 => Pcre2ErrorKind.HeapLimit, _ => Pcre2ErrorKind.ResultLimit }, error.ErrorKind);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AliasesKeepOriginalInterruptionAndRetainedCaptures(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"(?ir)(?<x>[ks]+)", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, TimeSpan.FromMilliseconds(20));
        var input = "😀kS Ks"u8.ToArray();
        var retained = regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, cancellation.Token);
        var cursor = regex.EnumerateMatches(input, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.True(cursor.MoveNext()); var alias = cursor;
        if (cancel) cancellation.Cancel(); else ticks = 25;
        Exception? failure = null;
        try { _ = alias.MoveNext(); } catch (Exception error) { failure = error; }
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal("kS", retained.GetGroup(1).GetValueString());
        Assert.Equal((4, 6), (retained.GetGroup(1).StartOffsetInBytes, retained.GetGroup(1).EndOffsetInBytes));
        Assert.True(regex.IsMatch("K"u8));
    }
}

using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2ProfileResourceTests
{
    [Theory]
    [InlineData(@"\x{41}+", "AAA")]
    [InlineData(@"\p{L}+", "αβ")]
    [InlineData("(?<a>x)(?<b>y)", "xy")]
    public void SingleAndDetailedMatchingEnforceProfileWork(string pattern, string text)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(input)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.Match(input)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed(input)).ErrorKind);
    }

    [Fact]
    public void CapturedExecutionFramesEnforceDepthBeforeAcquisition()
    {
        const string pattern = @"(?<open>A)+B+";
        Assert.True(new Utf8Pcre2Regex(pattern).IsMatch("AAB"u8));
        var limited = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { DepthLimit = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.DepthLimit, Assert.Throws<Pcre2MatchException>(() => limited.IsMatch("AAB"u8)).ErrorKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NoMatchOutputCannotEscapeTheHeapLimit(bool text)
    {
        var regex = new Utf8Pcre2Regex(@"\x{41}+", Pcre2CompileOptions.None, default,
            new() { HeapLimitInBytes = 4096 }, Timeout.InfiniteTimeSpan);
        var input = new byte[1 << 20]; input.AsSpan().Fill((byte)'z');
        var failure = Assert.Throws<Pcre2MatchException>(() =>
        {
            if (text) _ = System.Text.Encoding.UTF8.GetString(regex.Replace(input, "$0"));
            else _ = regex.Replace(input, "$0");
        });
        Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
    }

    [Fact]
    public void ProfileEmptyRetryUsesTheNonemptyAlternativeBeforeAdvancing()
    {
        var regex = new Utf8Pcre2Regex(@"\x{41}*?");
        var cursor = regex.EnumerateMatches("A😀"u8);
        var ranges = new List<(int Start, int End)>();
        while (cursor.MoveNext()) ranges.Add((cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.Equal(new[] { (0, 0), (0, 1), (1, 1), (5, 5) }, ranges);
        Assert.Equal(4, regex.Count("A😀"u8));
        Assert.Equal("!!!😀!", System.Text.Encoding.UTF8.GetString(regex.Replace("A😀"u8, "!")));
    }

    [Fact]
    public void MixedNamesAndRetainedFinalCapturesKeepTheirPublicCoordinates()
    {
        var regex = new Utf8Pcre2Regex(@"\x{41}(?<face>😀)(é)");
        var context = regex.MatchDetailed("xA😀éA😀é"u8, 1);
        Assert.Equal(3, context.CaptureSlotCount);
        Assert.Equal("A😀é", System.Text.Encoding.UTF8.GetString(context.Value.GetValueBytes()));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(context.GetGroup(1).GetValueBytes()));
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString(context.GetGroup(2).GetValueBytes()));
        Assert.True(context.TryGetFirstSetGroup("face", out var named));
        Assert.Equal((2, 6), (named.StartOffsetInBytes, named.EndOffsetInBytes));
        Assert.True(regex.MatchDetailed("A😀é"u8).Success);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));

        var repeated = new Utf8Pcre2Regex("(?<a>😀)+é").MatchDetailed("😀😀é"u8);
        Assert.True(repeated.TryGetFirstSetGroup("a", out var retained));
        Assert.Equal((4, 8), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
        var shared = new Utf8Pcre2Regex(@"(?<v>\x{41})(?<v>😀)", Pcre2CompileOptions.None,
            new() { AllowDuplicateNames = true }, default, Timeout.InfiniteTimeSpan).MatchDetailed("A😀"u8);
        Assert.True(shared.TryGetFirstSetGroup("v", out var first));
        Assert.Equal("A", System.Text.Encoding.UTF8.GetString(first.GetValueBytes()));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(shared.GetGroup(2).GetValueBytes()));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TemplateDiscoveryReplayAndOutputHaveOneFiniteAllowance(bool heap)
    {
        string Invoke(ulong cap)
        {
            var regex = new Utf8Pcre2Regex(@"(?<v>\x{41})😀", Pcre2CompileOptions.None, default,
                new() { MatchLimit = heap ? 0 : (uint)cap, HeapLimitInBytes = heap ? cap : 0 }, Timeout.InfiniteTimeSpan);
            return System.Text.Encoding.UTF8.GetString(regex.Replace("A😀éA😀"u8, "${v}:😀"));
        }
        var kind = heap ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.MatchLimit;
        const string expected = "A:😀éA:😀";
        Assert.Equal(expected, Invoke(1UL << 20));
        ulong first = 1, last = 1UL << 20;
        while (first < last)
        {
            var cap = first + (last - first) / 2;
            try { Assert.Equal(expected, Invoke(cap)); last = cap; }
            catch (Pcre2MatchException error) { Assert.Equal(kind, error.ErrorKind); first = cap + 1; }
        }
        Assert.InRange(first, 2UL, (1UL << 20) - 1);
        Assert.Equal(expected, Invoke(first));
        Assert.Equal(kind, Assert.Throws<Pcre2MatchException>(() => Invoke(first - 1)).ErrorKind);
        Assert.Equal(expected, Invoke(first));
    }

    [Fact]
    public void CopiedCursorKeepsIndependentPositionAndSharedStickyAllowance()
    {
        var regex = new Utf8Pcre2Regex(@"\x{41}", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 16 }, Timeout.InfiniteTimeSpan);
        var cursor = regex.EnumerateMatches("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"u8);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        Assert.Equal(0, alias.Current.StartOffsetInBytes);
        Assert.True(alias.MoveNext());
        Assert.Equal(cursor.Current.StartOffsetInBytes, alias.Current.StartOffsetInBytes);
        Pcre2MatchException? failure = null;
        try { while (cursor.MoveNext()) { } }
        catch (Pcre2MatchException error) { failure = error; }
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        failure = null;
        try { _ = alias.MoveNext(); }
        catch (Pcre2MatchException error) { failure = error; }
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal("A", System.Text.Encoding.UTF8.GetString(retained.GetValueBytes()));
        Assert.True(regex.Match("A"u8).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionSurvivesCursorWaitsAndAliases(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"\x{41}", Pcre2CompileOptions.None, default,
            new() { HeapLimitInBytes = 1000000 }, TimeSpan.FromMilliseconds(20));
        var cursor = regex.EnumerateMatches("AA"u8, 0, Pcre2MatchOptions.None, source.Token);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        var alias = cursor;
        if (cancel) source.Cancel(); else ticks = 25;
        Exception? failure = null;
        try { _ = alias.MoveNext(); } catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal("A", System.Text.Encoding.UTF8.GetString(retained.GetValueBytes()));
    }

    [Fact]
    public void ExtendedNativeTemplatesRetainGrammarAndFiniteControls()
    {
        const string template = @"\Ué\E$0";
        Assert.Equal("Éx", System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex("x").Replace("x"u8, template, 0, Pcre2SubstitutionOptions.Extended)));
        foreach (var heap in new[] { false, true })
        {
            var limited = new Utf8Pcre2Regex("x", Pcre2CompileOptions.None, default,
                new() { MatchLimit = heap ? 0u : 1u, HeapLimitInBytes = heap ? 1UL : 0UL }, Timeout.InfiniteTimeSpan);
            Assert.Equal(heap ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.MatchLimit,
                Assert.Throws<Pcre2MatchException>(() => System.Text.Encoding.UTF8.GetString(limited.Replace("x"u8, template, 0, Pcre2SubstitutionOptions.Extended))).ErrorKind);
        }
    }

    [Fact]
    public void ExpiredNativePublicationReportsTheProfileTimeout()
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, TimeSpan.FromMilliseconds(20), CancellationToken.None);
        var budget = new Pcre2ResourceBudget(default, invocation, false);
        var input = Utf8ValidatedInput.Create("éx"u8);
        ticks = 25;
        Pcre2MatchException? failure = null;
        try { Pcre2MatchPublication.Publish(2, 3, ref input, ref budget); }
        catch (Pcre2MatchException error) { failure = error; }
        Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.Budget.LimitBreach);
    }
}

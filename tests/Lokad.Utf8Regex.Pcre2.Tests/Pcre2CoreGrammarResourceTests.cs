using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2CoreGrammarResourceTests
{
    [Theory]
    [InlineData(@"\u0041+", "AAA")]
    [InlineData(@"\p{IsGreek}+", "αβ")]
    [InlineData("(?<a>x)(?<b-a>y)", "xy")]
    public void SingleAndDetailedMatchingEnforceCoreGrammarWork(string pattern, string text)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1 }, Timeout.InfiniteTimeSpan);
        Assert.IsType<Pcre2Utf8DirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(input)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.Match(input)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed(input)).ErrorKind);
    }

    [Fact]
    public void CoreGrammarExecutionFramesEnforceDepthBeforeAcquisition()
    {
        const string pattern = @"(?<open>\u0041)+(?<-open>B)+";
        Assert.True(new Utf8Pcre2Regex(pattern).IsMatch("AAB"u8));
        var limited = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { DepthLimit = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.DepthLimit, Assert.Throws<Pcre2MatchException>(() => limited.IsMatch("AAB"u8)).ErrorKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NoMatchOutputCannotEscapeTheHeapLimit(bool text)
    {
        var regex = new Utf8Pcre2Regex(@"\u0041+", Pcre2CompileOptions.None, default,
            new() { HeapLimitInBytes = 4096 }, Timeout.InfiniteTimeSpan);
        var input = new byte[1 << 20]; input.AsSpan().Fill((byte)'z');
        var failure = Assert.Throws<Pcre2MatchException>(() =>
        {
            if (text) _ = regex.ReplaceToString(input, "$0");
            else _ = regex.Replace(input, "$0");
        });
        Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
    }

    [Fact]
    public void CoreGrammarEmptyRetryUsesTheNonemptyAlternativeBeforeAdvancing()
    {
        var regex = new Utf8Pcre2Regex(@"\u0041*?");
        var cursor = regex.EnumerateMatches("A😀"u8);
        var ranges = new List<(int Start, int End)>();
        while (cursor.MoveNext()) ranges.Add((cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.Equal(new[] { (0, 0), (0, 1), (1, 1), (5, 5) }, ranges);
        Assert.Equal(4, regex.Count("A😀"u8));
        Assert.Equal("!!!😀!", regex.ReplaceToString("A😀"u8, "!"));
    }

    [Fact]
    public void MixedNamesAndRetainedFinalCapturesKeepTheirPublicCoordinates()
    {
        var regex = new Utf8Pcre2Regex(@"\u0041(?<face>😀)(é)");
        var context = regex.MatchDetailed("xA😀éA😀é"u8, 1);
        Assert.Equal(3, context.CaptureSlotCount);
        Assert.Equal(3, context.NameEntryCount);
        Assert.Equal("A😀é", context.GetValueString());
        Assert.Equal("é", context.GetGroup(1).GetValueString());
        Assert.True(context.TryGetFirstSetGroup("1", out var anonymous));
        Assert.Equal("é", anonymous.GetValueString());
        Assert.True(context.TryGetFirstSetGroup("face", out var named));
        Assert.Equal((2, 6), (named.StartOffsetInBytes, named.EndOffsetInBytes));
        Assert.Equal((2, 4), (named.StartOffsetInUtf16, named.EndOffsetInUtf16));
        Assert.True(regex.MatchDetailed("A😀é"u8).Success);
        Assert.Equal("😀", named.GetValueString());

        var balance = new Utf8Pcre2Regex("(?<a>😀)+(?<-a>é)").MatchDetailed("😀😀é"u8);
        Assert.True(balance.TryGetFirstSetGroup("a", out var restored));
        Assert.Equal((0, 4), (restored.StartOffsetInBytes, restored.EndOffsetInBytes));
        Assert.False(new Utf8Pcre2Regex("(?<a>😀)(?<-a>é)").MatchDetailed("😀é"u8).GetGroup(1).Success);
        var shared = new Utf8Pcre2Regex(@"(?<v>\u0041)(?<v>😀)", Pcre2CompileOptions.None,
            new() { AllowDuplicateNames = true }, default, Timeout.InfiniteTimeSpan).MatchDetailed("A😀"u8);
        Assert.True(shared.TryGetFirstSetGroup("v", out var last));
        Assert.Equal("😀", last.GetValueString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TemplateDiscoveryReplayAndOutputHaveOneFiniteAllowance(bool heap)
    {
        string Invoke(ulong cap)
        {
            var regex = new Utf8Pcre2Regex(@"(?<v>\u0041)😀", Pcre2CompileOptions.None, default,
                new() { MatchLimit = heap ? 0 : (uint)cap, HeapLimitInBytes = heap ? cap : 0 }, Timeout.InfiniteTimeSpan);
            Assert.IsType<Pcre2Utf8DirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
            return regex.ReplaceToString("A😀éA😀"u8, "${v}:😀");
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
        var regex = new Utf8Pcre2Regex(@"\u0041", Pcre2CompileOptions.None, default,
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
        Assert.Equal("A", retained.GetValueString());
        Assert.True(regex.Match("A"u8).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionSurvivesCursorWaitsAndAliases(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"\u0041", Pcre2CompileOptions.None, default,
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
        Assert.Equal("A", retained.GetValueString());
    }

    [Fact]
    public void CallbackCancellationUsesTheOriginalTokenAndLeavesRetainedCaptureValid()
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"(?<v>\u0041)😀");
        var state = new CallbackState(source);
        var failure = Assert.Throws<OperationCanceledException>(() => regex.Replace("A😀A😀"u8, state,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref CallbackState state) =>
            {
                Assert.True(match.TryGetFirstSetGroup("v", out var capture));
                state.Capture = capture._data;
                state.Calls++;
                writer.Append("!"u8);
                state.Source.Cancel();
            }, 0, Pcre2MatchOptions.None, source.Token));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(1, state.Calls);
        Assert.Equal((0, 1), (state.Capture.StartOffsetInBytes, state.Capture.EndOffsetInBytes));
        Assert.Equal("A😀", regex.MatchDetailed("A😀"u8).GetValueString());
    }

    [Fact]
    public void ExtendedNativeTemplatesRetainGrammarAndFiniteControls()
    {
        const string template = @"\Ué\E$0";
        Assert.Equal("Éx", new Utf8Pcre2Regex("x").ReplaceToString("x"u8, template, 0, Pcre2SubstitutionOptions.Extended));
        foreach (var heap in new[] { false, true })
        {
            var limited = new Utf8Pcre2Regex("x", Pcre2CompileOptions.None, default,
                new() { MatchLimit = heap ? 0u : 1u, HeapLimitInBytes = heap ? 1UL : 0UL }, Timeout.InfiniteTimeSpan);
            Assert.Equal(heap ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.MatchLimit,
                Assert.Throws<Pcre2MatchException>(() => limited.ReplaceToString("x"u8, template, 0, Pcre2SubstitutionOptions.Extended)).ErrorKind);
        }
    }

    private sealed class CallbackState(CancellationTokenSource source)
    {
        internal CancellationTokenSource Source = source;
        internal Pcre2GroupData Capture;
        internal int Calls;
    }
}

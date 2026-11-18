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
    public void PrimaryAndSnapshotMatchingEnforceProfileWork(string pattern, string text)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(input)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.Match(input)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        {
            ReadOnlyMemory<byte> result1Input = input;
            using var result1Cursor = regex.CreateMatchCursor(result1Input);
            result1Cursor.MoveNext(out var result1Result);
            result1Cursor.Dispose();
            _ = result1Result;
        }).ErrorKind);
    }

    [Fact]
    public void CapturedExecutionFramesEnforceDepthBeforeAcquisition()
    {
        const string pattern = @"(?<open>A)+B+";
        Assert.True(new Utf8Pcre2Regex(pattern).IsMatch("AAB"u8));
        var limited = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.DepthLimit, Assert.Throws<Pcre2MatchException>(() => limited.IsMatch("AAB"u8)).ErrorKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoMatchOutputCannotEscapeTheHeapLimit(bool text)
    {
        var regex = new Utf8Pcre2Regex(@"\x{41}+", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 4096
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = new byte[1 << 20]; input.AsSpan().Fill((byte)'z');
        var failure = Assert.Throws<Pcre2MatchException>(() =>
        {
            if (text) _ = System.Text.Encoding.UTF8.GetString(regex.Replace(input, "$0"u8));
            else _ = regex.Replace(input, "$0"u8);
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
        Assert.Equal("!!!😀!", System.Text.Encoding.UTF8.GetString(regex.Replace("A😀"u8, "!"u8)));
    }

    [Fact]
    public void MixedNamesAndRetainedFinalCapturesKeepTheirPublicCoordinates()
    {
        var regex = new Utf8Pcre2Regex(@"\x{41}(?<face>😀)(é)");
        ReadOnlyMemory<byte> context2Input = ("xA😀éA😀é"u8).ToArray();
        using var context2Cursor = regex.CreateMatchCursor(context2Input, 1);
        context2Cursor.MoveNext(out var context2Result);
        context2Cursor.Dispose();
        var context = context2Result;
        Assert.Equal(3, context.Groups.Length);
        Assert.Equal("A😀é", System.Text.Encoding.UTF8.GetString((context.Value.Success ? context2Input.Span.Slice(context.Value.StartOffsetInBytes, context.Value.EndOffsetInBytes - context.Value.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((context.Groups[1].Success ? context2Input.Span.Slice(context.Groups[1].StartOffsetInBytes, context.Groups[1].EndOffsetInBytes - context.Groups[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString((context.Groups[2].Success ? context2Input.Span.Slice(context.Groups[2].StartOffsetInBytes, context.Groups[2].EndOffsetInBytes - context.Groups[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.True(context.TryGetGroup(1, out var named)); Assert.True(named.Success);
        Assert.Equal((2, 6), (named.StartOffsetInBytes, named.EndOffsetInBytes));
        ReadOnlyMemory<byte> result3Input = ("A😀é"u8).ToArray();
        using var result3Cursor = regex.CreateMatchCursor(result3Input);
        result3Cursor.MoveNext(out var result3Result);
        result3Cursor.Dispose();
        Assert.True(result3Result.Success);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((named.Success ? context2Input.Span.Slice(named.StartOffsetInBytes, named.EndOffsetInBytes - named.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        var repeated4Regex = new Utf8Pcre2Regex("(?<a>😀)+é");
        ReadOnlyMemory<byte> repeated4Input = ("😀😀é"u8).ToArray();
        using var repeated4Cursor = repeated4Regex.CreateMatchCursor(repeated4Input);
        repeated4Cursor.MoveNext(out var repeated4Result);
        repeated4Cursor.Dispose();

        var repeated = repeated4Result;
        Assert.True(repeated.TryGetGroup(1, out var retained)); Assert.True(retained.Success);
        Assert.Equal((4, 8), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
        var shared5Regex = new Utf8Pcre2Regex(@"(?<v>\x{41})(?<v>😀)", options: Pcre2CompileOptions.None, compileSettings: new() { AllowDuplicateNames = true }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        ReadOnlyMemory<byte> shared5Input = ("A😀"u8).ToArray();
        using var shared5Cursor = shared5Regex.CreateMatchCursor(shared5Input);
        shared5Cursor.MoveNext(out var shared5Result);
        shared5Cursor.Dispose();
        var shared = shared5Result;
        Assert.True(shared.TryGetGroup(1, out var first)); Assert.True(first.Success);
        Assert.Equal("A", System.Text.Encoding.UTF8.GetString((first.Success ? shared5Input.Span.Slice(first.StartOffsetInBytes, first.EndOffsetInBytes - first.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((shared.Groups[2].Success ? shared5Input.Span.Slice(shared.Groups[2].StartOffsetInBytes, shared.Groups[2].EndOffsetInBytes - shared.Groups[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TemplateDiscoveryReplayAndOutputHaveOneFiniteAllowance(bool heap)
    {
        string Invoke(ulong cap)
        {
            var regex = new Utf8Pcre2Regex(@"(?<v>\x{41})😀", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxWorkSteps = (heap ? 0 : (uint)cap) == 0 ? null : (heap ? 0 : (uint)cap),
                    MaxWorkingBytes = (heap ? cap : 0) == 0 ? null : (heap ? cap : 0)
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            });
            return System.Text.Encoding.UTF8.GetString(regex.Replace("A😀éA😀"u8, "${v}:😀"u8));
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
        var regex = new Utf8Pcre2Regex(@"\x{41}", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 16
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
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
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInterruptionSurvivesCursorWaitsAndAliases(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"\x{41}", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1000000
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
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
        Assert.Equal("Éx", System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex("x").Replace("x"u8, "\\Ué\\E$0"u8, 0, Pcre2SubstitutionOptions.Extended)));
        foreach (var heap in new[] { false, true })
        {
            var limited = new Utf8Pcre2Regex("x", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxWorkSteps = (heap ? 0u : 1u) == 0 ? null : (heap ? 0u : 1u),
                    MaxWorkingBytes = (heap ? 1UL : 0UL) == 0 ? null : (heap ? 1UL : 0UL)
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            });
            Assert.Equal(heap ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.MatchLimit,
                Assert.Throws<Pcre2MatchException>(() => System.Text.Encoding.UTF8.GetString(limited.Replace("x"u8, "\\Ué\\E$0"u8, 0, Pcre2SubstitutionOptions.Extended))).ErrorKind);
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

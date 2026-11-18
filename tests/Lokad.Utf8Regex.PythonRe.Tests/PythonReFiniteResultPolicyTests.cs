using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteResultPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(21)]
    public void ByteBoundedTimeoutOmitsSubjectAcrossPreparationAndResultOwners(int operation)
    {
        var input = Enumerable.Repeat((byte)'x', 200_000).ToArray();
        foreach (var kind in new[] { 0, 1, 2 })
        {
            var policy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = kind != 1 ? 1_048_576ul : null,
                MaxResultBytes = kind != 0 ? 1_048_576ul : null
            };
            var regex = new Utf8PythonRegex("z", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1), policy);
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
            var error = Assert.Throws<RegexMatchTimeoutException>(() =>
                PythonReFiniteWorkPolicyTests.Run(regex, input, operation));
            Assert.Empty(error.Input);
            Assert.Equal("z", error.Pattern);
            Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
        }
    }

    [Fact]
    public void CursorTimeoutKeepsPriorSnapshotAndReleasesScratchWithoutDiagnosticCopy()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var regex = new Utf8PythonRegex(".", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(200),
            new() { MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 });
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cursor = regex.CreateMatchCursor("😀a"u8.ToArray());
        Assert.True(cursor.MoveNext(out cursorRange));
        var retained = cursorRange;
        var invocation = cursor.WorkingInvocation;
        ticks = 250;
        var error = Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _));
        Assert.Empty(error.Input);
        Assert.Equal(regex.Pattern, error.Pattern);
        Assert.Equal(TimeSpan.FromMilliseconds(200), error.MatchTimeout);
        Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        cursor.Dispose();
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Fact]
    public void WorkOnlyTimeoutRetainsInputAndPreCancellationRetainsToken()
    {
        var regex = new Utf8PythonRegex("z", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1),
            new() { MaxWorkSteps = ulong.MaxValue });
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch("xxxxxxxx"u8));
        Assert.Equal("xxxxxxxx", error.Input);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = Assert.Throws<OperationCanceledException>(() => regex.IsMatch("x"u8, 0, cancellation.Token));
        Assert.Equal(cancellation.Token, canceled.CancellationToken);
    }

    public static IEnumerable<object[]> Operations() => PythonReFiniteWorkPolicyTests.Operations();
    private static Utf8PythonRegex Create(string pattern, ulong cap, bool bytes = false, ulong? work = null,
        PythonReCompileOptions options = PythonReCompileOptions.None, TimeSpan? timeout = null)
    {
        var policy = new Utf8ResourcePolicy { MaxResultBytes = cap, MaxWorkSteps = work };
        return bytes ? new(Encoding.UTF8.GetBytes(pattern), options, timeout ?? Regex.InfiniteMatchTimeout, policy)
            : new(pattern, options, timeout ?? Regex.InfiniteMatchTimeout, policy);
    }
    private static string Run(Utf8PythonRegex regex, byte[] input, int operation, int start = 0, CancellationToken token = default)
        => PythonReFiniteWorkPolicyTests.Run(regex, input, operation, start, token);
    private static void Check(Utf8ResourceLimitExceededException error, ulong cap)
    {
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
        Assert.Equal(cap, error.Limit);
        Assert.True(error.Requested > cap || cap == ulong.MaxValue && error.Requested == cap);
    }
    private static void Denied(Action action, ulong cap) => Check(Assert.Throws<Utf8ResourceLimitExceededException>(action), cap);

    [Theory]
    [MemberData(nameof(Operations))]
    public void AllOperationsPreserveTheirResultsWithFiniteCapacity(int operation, string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = Run(new(pattern), input, operation);
        using var source = new CancellationTokenSource();
        foreach (var bytes in new[] { false, true })
            foreach (var token in new[] { default, source.Token })
            {
                Assert.Equal(expected, Run(Create(pattern, 1000000, bytes), input, operation, token: token));
                Assert.Equal(expected, Run(Create(pattern, ulong.MaxValue, bytes), input, operation, token: token));
                Assert.Equal(expected, Run(Create(pattern, 1000000, bytes, work: 1000000), input, operation, token: token));
            }
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void ExactOneUnderAndEverySmallCapacityHaveExplicitOutcomes(int operation, string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = Run(Create(pattern, 1000000), input, operation);
        ulong first = 1, last = 1000000;
        while (first < last)
        {
            var cap = first + (last - first) / 2;
            try { Assert.Equal(expected, Run(Create(pattern, cap), input, operation)); last = cap; }
            catch (Utf8ResourceLimitExceededException error) { Check(error, cap); first = cap + 1; }
        }
        Assert.Equal(expected, Run(Create(pattern, first), input, operation));
        if (first > 1) Denied(() => Run(Create(pattern, first - 1), input, operation), first - 1);
        for (ulong cap = 1; cap <= 64; cap++)
        {
            if (cap >= first) Assert.Equal(expected, Run(Create(pattern, cap), input, operation));
            else Denied(() => Run(Create(pattern, cap), input, operation), cap);
        }
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void OriginalCancellationAndCombinedWorkRemainAuthoritative(int operation, string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text).Concat(new byte[] { 255 }).ToArray();

        using var source = new CancellationTokenSource(); source.Cancel();
        var canceled = Assert.Throws<OperationCanceledException>(() => Run(Create(pattern, 1), input, operation, token: source.Token));
        Assert.Equal(source.Token, canceled.CancellationToken);
        var valid = Encoding.UTF8.GetBytes(text);
        var work = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(Create(pattern, 1000000, work: 1), valid, operation));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, work.ResourceKind);
        Assert.Equal(1UL, work.Limit);
        Assert.Equal(2UL, work.Requested);
    }

    [Theory]
    [InlineData(PythonReCompileOptions.None)]
    [InlineData(PythonReCompileOptions.IgnoreCase)]
    [InlineData(PythonReCompileOptions.Multiline)]
    [InlineData(PythonReCompileOptions.DotAll)]
    [InlineData(PythonReCompileOptions.Verbose)]
    [InlineData(PythonReCompileOptions.Ascii)]
    [InlineData(PythonReCompileOptions.Unicode)]
    public void SupportedFlagsKeepScalarCoordinatesAndValues(PythonReCompileOptions options)
    {
        const string pattern = "😀|(?P<x>a)";
        var input = "😀a\nA"u8.ToArray();
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = new Utf8PythonRegex(pattern, options).CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var expected = collectedRanges1.ToArray();
        var collectedRanges2 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor2 = Create(pattern, 1000000, options: options).CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor2.MoveNext(out var rangeMatch2))
                collectedRanges2.Add(rangeMatch2);
        }
        var actual = collectedRanges2.ToArray();
        Assert.Equal(expected.Select(x => System.Text.Encoding.UTF8.GetString(x.GetValueMemory().Span)), actual.Select(x => System.Text.Encoding.UTF8.GetString(x.GetValueMemory().Span)));
        Assert.Equal(expected.Select(x => x.Value.StartOffsetInBytes), actual.Select(x => x.Value.StartOffsetInBytes));
        Assert.Equal(4, actual[0].Value.EndOffsetInBytes);
    }

    [Fact]
    public void TupleCaptureNameAndSplitShapesAreOwnedAndRetained()
    {
        var input = "😀a"u8.ToArray();
        var regex = Create("(?P<first>😀)|(?P<second>a)", 1000000);
        var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input));
        Assert.Equal(new[] { new[] { "😀", "" }, new[] { "", "a" } }, strings.TupleValues);
        var bytes = regex.FindAllToUtf8(input);
        Assert.Equal("😀"u8.ToArray(), bytes.TupleValues[0][0]);
        Assert.Empty(bytes.TupleValues[0][1]);
        Assert.Empty(bytes.TupleValues[1][0]);
        Assert.Equal("a"u8.ToArray(), bytes.TupleValues[1][1]);
        Utf8PythonRangeMatch retained = default;
        using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor1.MoveNext(out retained);
        }
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.True(retained.TryGetFirstSetGroup("first", out var first));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((first.Success ? retained.GetGroupMemory(first.Number).Span : ReadOnlySpan<byte>.Empty)));
        Denied(() => PythonReFindAllAssert.Decode(Create("(?P<first>😀)|(?P<second>a)", 1).FindAllToUtf8(input)), 1);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.Equal(new string?[] { "", "😀", null, "", null, "a", "" }, PythonReSplitAssert.Decode(input, regex.SplitRanges(input, 0, 0, default)));
    }

    [Fact]
    public void CursorTrafficSurvivesPublicationAndAliasesShareFailureCleanup()
    {
        Utf8PythonRangeMatch cursorRange = default;
        Utf8PythonRangeMatch cursorRange_fresh = default;
        var input = "😀a"u8.ToArray();
        ulong firstBytes;
        using (var probe = Create("(?P<x>.)", ulong.MaxValue).CreateMatchCursor(input))
        {
            Assert.True(probe.MoveNext(out _)); firstBytes = probe.WorkingInvocation.Budget.ResultBytes;
            Assert.True(probe.MoveNext(out _)); Assert.True(probe.WorkingInvocation.Budget.ResultBytes > firstBytes);
        }
        var regex = Create("(?P<x>.)", firstBytes);
        using var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        var authority = cursor.WorkingInvocation;
        Assert.True(alias.MoveNext(out cursorRange)); var retained = cursorRange;
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Denied(() => cursor.MoveNext(out cursorRange), firstBytes);
        Assert.Equal(0UL, authority.Ledger.ReservedBytes);
        Assert.True(authority.Budget.ResultBytes > firstBytes);
        Assert.Throws<InvalidOperationException>(() => alias.MoveNext(out _));
        alias.Dispose(); cursor.Dispose();
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));

        // The instance policy is per invocation, not a shared host allowance.
        // A fresh cursor can publish again while the first snapshot is retained.
        using var fresh = regex.CreateMatchCursor(input);
        Assert.True(fresh.MoveNext(out cursorRange_fresh));
        var secondRetained = cursorRange_fresh;
        Assert.Equal(firstBytes, fresh.WorkingInvocation.Budget.ResultBytes);
        fresh.Dispose();
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(secondRetained.GetValueMemory().Span));
    }

    [Fact]
    public void EmptyRetryFixedValuesMissingResultsAndStandaloneGettersKeepTheirScope()
    {
        Assert.True(Create(".", 1).IsMatch("😀"u8));
        Assert.Equal(2, Create(".", 1).Count("😀a"u8));
        var range = Create(".", 1).Match("😀"u8,mode: Utf8PythonMatchMode.Search);
        Assert.Equal(4, range.EndOffsetInBytes);
        Denied(() =>
        {
            Utf8PythonRangeMatch snapshot2 = default;
            using (var captureCursor2 = Create(".", 1).CreateMatchCursor(("😀"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
            {
                captureCursor2.MoveNext(out snapshot2);
            }

            _ = snapshot2;
        }, 1);
        PythonReMatchAssert.Equal("", Create("", 1).Match("😀"u8,mode: Utf8PythonMatchMode.Search));
        Assert.False(Create("missing", 1).Match("😀"u8,mode: Utf8PythonMatchMode.Search).Success);
        var regex = Create(".*?", 1000000);
        var collectedRanges3 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor3 = regex.CreateMatchCursor(("😀a"u8).ToArray(), 0, default))
        {
            while (rangeCursor3.MoveNext(out var rangeMatch3))
                collectedRanges3.Add(rangeMatch3);
        }
        Assert.Equal(new[] { "", "😀", "", "a", "" }, collectedRanges3.ToArray().Select(x => System.Text.Encoding.UTF8.GetString(x.GetValueMemory().Span)));
        Assert.Equal(4, regex.Match("😀a"u8,4,mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("😀a"u8,1,mode: Utf8PythonMatchMode.Search));
        Utf8PythonRangeMatch context = default;
        using (var captureCursor3 = regex.CreateMatchCursor(("😀a"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor3.MoveNext(out context);
        }
        Assert.Equal("", System.Text.Encoding.UTF8.GetString(context.GetValueMemory().Span));
        Assert.NotEmpty(Create("(?P<x>a)", 1).GetGroupNames());
    }

    [Fact]
    public void OriginalCursorDeadlineWinsAfterAHostWait()
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cursor = Create(".", 1, timeout: TimeSpan.FromMilliseconds(200)).CreateMatchCursor("😀"u8.ToArray());
        ticks = 250;
        Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _));
    }

    [Fact]
    public void ZeroAndUnsupportedFieldsRejectBeforePatternWork()
    {
        var zero = new Utf8ResourcePolicy { MaxResultBytes = 0 };
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PythonRegex("[invalid", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PythonRegex(new byte[] { 255 }, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, zero));
        var unsupported = new Utf8ResourcePolicy { MaxResultBytes = 1000000, MaxWorkingBytes = 1 };
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex("[invalid", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, unsupported));
    }
}

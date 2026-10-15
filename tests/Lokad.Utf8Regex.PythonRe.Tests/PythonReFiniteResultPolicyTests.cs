using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteResultPolicyTests
{
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
    public void FullValidationOriginalCancellationAndCombinedWorkRemainAuthoritative(int operation, string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text).Concat(new byte[] { 255 }).ToArray();
        Assert.Throws<ArgumentException>(() => Run(Create(pattern, 1000000), input, operation));
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
    [InlineData(PythonReCompileOptions.None)] [InlineData(PythonReCompileOptions.IgnoreCase)]
    [InlineData(PythonReCompileOptions.Multiline)] [InlineData(PythonReCompileOptions.DotAll)]
    [InlineData(PythonReCompileOptions.Verbose)] [InlineData(PythonReCompileOptions.Ascii)]
    [InlineData(PythonReCompileOptions.Unicode)]
    public void SupportedFlagsKeepScalarCoordinatesAndValues(PythonReCompileOptions options)
    {
        const string pattern = "😀|(?P<x>a)";
        var input = "😀a\nA"u8.ToArray();
        var expected = new Utf8PythonRegex(pattern, options).FindIterDetailed(input);
        var actual = Create(pattern, 1000000, options: options).FindIterDetailed(input);
        Assert.Equal(expected.Select(x => x.Value.ValueText), actual.Select(x => x.Value.ValueText));
        Assert.Equal(expected.Select(x => x.Value.StartOffsetInBytes), actual.Select(x => x.Value.StartOffsetInBytes));
        Assert.Equal(expected.Select(x => x.Value.EndOffsetInUtf16), actual.Select(x => x.Value.EndOffsetInUtf16));
        Assert.Equal(4, actual[0].Value.EndOffsetInBytes);
    }

    [Fact]
    public void TupleCaptureNameAndSplitShapesAreOwnedAndRetained()
    {
        var input = "😀a"u8.ToArray();
        var regex = Create("(?P<first>😀)|(?P<second>a)", 1000000);
        var strings = regex.FindAllToStrings(input);
        Assert.Equal(new[] { new[] { "😀", "" }, new[] { "", "a" } }, strings.TupleValues);
        var bytes = regex.FindAllToUtf8(input);
        Assert.Equal("😀"u8.ToArray(), bytes.TupleValues[0][0]);
        Assert.Empty(bytes.TupleValues[0][1]);
        Assert.Empty(bytes.TupleValues[1][0]);
        Assert.Equal("a"u8.ToArray(), bytes.TupleValues[1][1]);
        var retained = regex.SearchDetailedData(input);
        Assert.Equal("😀", retained.Value.ValueText);
        Assert.True(retained.TryGetFirstSetGroup("first", out var first));
        Assert.Equal("😀", first.ValueText);
        Denied(() => Create("(?P<first>😀)|(?P<second>a)", 1).FindAllToStrings(input), 1);
        Assert.Equal("😀", retained.Value.ValueText);
        Assert.Equal(new string?[] { "", "😀", null, "", null, "a", "" }, regex.SplitToStrings(input));
    }

    [Fact]
    public void CursorTrafficSurvivesPublicationAndAliasesShareFailureCleanup()
    {
        var input = "😀a"u8.ToArray();
        ulong firstBytes;
        using (var probe = Create("(?P<x>.)", ulong.MaxValue).CreateMatchCursor(input))
        {
            Assert.True(probe.MoveNext()); firstBytes = probe.WorkingInvocation.Budget.ResultBytes;
            Assert.True(probe.MoveNext()); Assert.True(probe.WorkingInvocation.Budget.ResultBytes > firstBytes);
        }
        var regex = Create("(?P<x>.)", firstBytes);
        using var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        var authority = cursor.WorkingInvocation;
        Assert.True(alias.MoveNext()); var retained = cursor.Current;
        Assert.Equal("😀", retained.Value.ValueText);
        Denied(() => cursor.MoveNext(), firstBytes);
        Assert.Equal(0UL, authority.Ledger.ReservedBytes);
        Assert.True(authority.Budget.ResultBytes > firstBytes);
        Assert.Throws<InvalidOperationException>(() => alias.MoveNext());
        alias.Dispose(); cursor.Dispose();
        Assert.Equal("😀", retained.Value.ValueText);

        // The instance policy is per invocation, not a shared host allowance.
        // A fresh cursor can publish again while the first snapshot is retained.
        using var fresh = regex.CreateMatchCursor(input);
        Assert.True(fresh.MoveNext());
        var secondRetained = fresh.Current;
        Assert.Equal(firstBytes, fresh.WorkingInvocation.Budget.ResultBytes);
        Assert.NotSame(retained.Groups, secondRetained.Groups);
        fresh.Dispose();
        Assert.Equal("😀", retained.Value.ValueText);
        Assert.Equal("😀", secondRetained.Value.ValueText);
    }

    [Fact]
    public void EmptyRetryFixedValuesMissingResultsAndStandaloneGettersKeepTheirScope()
    {
        Assert.True(Create(".", 1).IsMatch("😀"u8));
        Assert.Equal(2, Create(".", 1).Count("😀a"u8));
        var range = Create(".", 1).Search("😀"u8);
        Assert.Equal(4, range.EndOffsetInBytes);
        Assert.Equal(2, range.EndOffsetInUtf16);
        Denied(() => Create(".", 1).SearchDetailedData("😀"u8), 1);
        Assert.Equal("", Create("", 1).SearchToString("😀"u8));
        Assert.Null(Create("missing", 1).SearchToString("😀"u8));
        var regex = Create(".*?", 1000000);
        Assert.Equal(new[] { "", "😀", "", "a", "" }, regex.FindIterDetailed("😀a"u8).Select(x => x.Value.ValueText));
        Assert.Equal(4, regex.Search("😀a"u8, 4).StartOffsetInBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Search("😀a"u8, 1));
        var context = regex.SearchDetailed("😀a"u8);
        Assert.Equal("", context.GetValueString());
        Assert.NotEmpty(Create("(?P<x>a)", 1).GetGroupNames());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CallbackExpansionUsesTheOriginalCapacityAndAllowsIndependentOperations(bool bytes)
    {
        var called = false;
        var regex = Create("a", 4096);
        if (bytes) Denied(() => regex.Subn("a"u8, 0, (_, _) =>
        { called = true; return new byte[1000000]; }, 0, 0), 4096);
        else Denied(() => regex.SubnToString("a"u8, 0, (_, _) =>
        { called = true; return new string('é', 1000000); }, 0, 0), 4096);
        Assert.True(called);
        Assert.Equal("small", regex.ReplaceToString("a"u8, "small"));
        Assert.Equal("small"u8.ToArray(), regex.Replace("a"u8, "small"));
    }

    [Fact]
    public void CallbackCancellationPrecedesTheReturnedExpansionDenial()
    {
        using var source = new CancellationTokenSource();
        var regex = Create("a", 4096);
        var canceled = Assert.Throws<OperationCanceledException>(() => regex.SubnToString("a"u8, source, (state, _) =>
        { state.Cancel(); return new string('x', 1000000); }, 0, 0, source.Token));
        Assert.Equal(source.Token, canceled.CancellationToken);
    }

    [Fact]
    public void OriginalCursorDeadlineWinsAfterAHostWait()
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cursor = Create(".", 1, timeout: TimeSpan.FromMilliseconds(200)).CreateMatchCursor("😀"u8.ToArray());
        ticks = 250;
        Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext());
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

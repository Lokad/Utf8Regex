using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteWorkPolicyTests
{
    internal static IEnumerable<int> OperationIds()
    {
        for (var operation = 0; operation < 25; operation++)
            if (operation is not (5 or 6 or 7 or 11 or 12 or 13 or 15 or 14 or 16 or 19 or 20 or 22 or 23 or 24)) yield return operation;
    }

    public static IEnumerable<object[]> Operations()
    {
        // Exact literal, two native-specialized forms, and general scalar discovery.
        foreach (var (pattern, input) in new[] { ("cat", "cat"), ("cat\\d+", "cat123"),
            ("(?s:cat.*dog)", "cat😀dog"), ("(?P<x>a|ab)+(?P=x)", "abab"), (".*?", "😀a"),
            ("foo(?=bar)", "foobar") })
            foreach (var operation in OperationIds())
                yield return [operation, pattern, input];
    }

    private static Utf8PythonRegex RegexWith(string pattern, ulong limit, bool bytesPattern = false)
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = limit };
        return bytesPattern
            ? new(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy)
            : new(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
    }

    internal static string Run(Utf8PythonRegex regex, byte[] input, int operation, int start = 0,
        CancellationToken token = default) => operation switch
    {
        0 => regex.IsMatch(input, start, token).ToString(),
        1 => regex.Search(input, start, token).Success.ToString(),
        2 => regex.Match(input, start, token).Success.ToString(),
        3 => regex.FullMatch(input, start, token).Success.ToString(),
        4 => regex.Count(input, start, token).ToString(),
        8 => regex.SearchDetailed(input, start, token).Success.ToString(),
        9 => regex.MatchDetailed(input, start, token).Success.ToString(),
        10 => regex.FullMatchDetailed(input, start, token).Success.ToString(),
        17 => regex.FindAllToUtf8(input, start, token).ScalarValues.Length.ToString(),
        18 => string.Join("|", regex.SplitRanges(input, 0, start, token).Select(x => $"{x.Number}:{x.Success}:{x.StartOffsetInBytes}:{x.EndOffsetInBytes}")),
        21 => Encoding.UTF8.GetString(regex.Subn(input, "<\\g<0>>", 0, start, token).ResultBytes),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    private static void Exhausted(Action action, ulong limit)
    {
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(action);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(limit, error.Limit);
        Assert.Equal(limit + 1, error.Requested);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryOperationEnforcesItsCapThroughCompletion(int operation, string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = Run(new(pattern), input, operation);
        Assert.Equal(expected, Run(RegexWith(pattern, 10000, bytesPattern: true), input, operation));
        Exhausted(() => Run(RegexWith(pattern, 1), input, operation), 1);
        // Locate an operation's full public cutoff, including required projection
        // and output. No timing assumptions or historical step count constants.
        ulong low = 1, high = 10000;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            try { Assert.Equal(expected, Run(RegexWith(pattern, middle), input, operation)); high = middle; }
            catch (Utf8ResourceLimitExceededException) { low = middle; }
        }
        Exhausted(() => Run(RegexWith(pattern, low), input, operation), low);
        Assert.Equal(expected, Run(RegexWith(pattern, high), input, operation));
        Assert.Equal(expected, Run(RegexWith(pattern, high + 1), input, operation));
    }

    [Theory]
    [InlineData("(a|ab)+(?=c)c", "ababc")]
    [InlineData("(?P<x>a|ab)+(?P=x)", "ababc")]
    [InlineData(".*?", "😀a")]
    public void EverySmallSearchCutoffEitherCompletesOrFailsExplicitly(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Utf8PythonRegex(pattern).SearchDetailed(input);
        for (ulong limit = 1; limit <= 4096; limit++)
        {
            try
            {
                var actual = RegexWith(pattern, limit).SearchDetailed(input);
                Assert.Equal(expected.Success, actual.Success);
                PythonReCaptureAssert.Equal(expected,actual);
                return;
            }
            catch (Utf8ResourceLimitExceededException error)
            {
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
                Assert.Equal(limit, error.Limit); Assert.Equal(limit + 1, error.Requested);
            }
        }
        Assert.Fail("The bounded fixture did not complete within 4096 steps.");
    }

    [Fact]
    public void CursorSharesItsCapAcrossMovesAndRetainedResultsSurviveFailure()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = "😀a"u8.ToArray();
        ulong firstSteps;
        using (var probe = RegexWith(".", ulong.MaxValue).CreateMatchCursor(input))
        {
            Assert.True(probe.MoveNext(out _)); firstSteps = probe.WorkingInvocation.Budget.Steps;
            Assert.True(probe.MoveNext(out _)); Assert.True(probe.WorkingInvocation.Budget.Steps > firstSteps);
        }
        using var cursor = RegexWith(".", firstSteps).CreateMatchCursor(input);
        var alias = cursor;
        var authority = cursor.WorkingInvocation;
        Assert.True(cursor.MoveNext(out cursorRange)); var retained = cursorRange;
        Assert.Equal(4, retained.Value.EndOffsetInBytes);
        Exhausted(() => alias.MoveNext(out cursorRange), firstSteps);
        Assert.Equal(0UL, authority.Ledger.ReservedBytes);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
        alias.Dispose(); cursor.Dispose();
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Fact]
    public void EmptyRetryAndSupplementaryOffsetsRemainScalar()
    {
        var regex = RegexWith(".*?", 10000);
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(("😀a"u8).ToArray(), 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var all = collectedRanges1.ToArray();
        Assert.Equal(new[] { "", "😀", "", "a", "" }, all.Select(x => System.Text.Encoding.UTF8.GetString(x.GetValueMemory().Span)));
        Assert.Equal(4, regex.Search("😀a"u8, 4).StartOffsetInBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Search("😀a"u8, 1));
        Assert.Equal(1, regex.Count("😀a"u8, 5));
    }

    [Theory]
    [InlineData(0)] [InlineData(4)]   [InlineData(21)]
    public void WorkCoversLateMisses(int operation)
    {
        var miss = Encoding.UTF8.GetBytes(new string('x', 16384));
        var expected = Run(new("cat"), miss, operation);
        Assert.Equal(expected, Run(RegexWith("cat", 10000), miss, operation));
        // Bounded prefix seeking can skip impossible starts, so the old
        // per-position cutoff of100 is no longer an admission contract.
        // Find the complete public cutoff and retain the exact denial boundary.
        ulong low = 1, high = 10000;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            try { Assert.Equal(expected, Run(RegexWith("cat", middle), miss, operation)); high = middle; }
            catch (Utf8ResourceLimitExceededException) { low = middle; }
        }
        Exhausted(() => Run(RegexWith("cat", low), miss, operation), low);
        Assert.Equal(expected, Run(RegexWith("cat", high), miss, operation));
    }

    [Fact]
    public void CancellationAndExpiredDeadlineKeepTheirIdentity()
    {
        using var token = new CancellationTokenSource(); token.Cancel();
        var regex = RegexWith("cat", 1);
        var error = Assert.Throws<OperationCanceledException>(() => regex.Search("cat"u8, 0, token.Token));
        Assert.Equal(token.Token, error.CancellationToken);
        Assert.Throws<OperationCanceledException>(() => regex.CreateMatchCursor("cat"u8.ToArray(), 0, token.Token));

        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks += 100);
        regex = new("cat", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1), new() { MaxWorkSteps = 1 });
        Assert.Throws<RegexMatchTimeoutException>(() => regex.Search("cat"u8));
    }
}

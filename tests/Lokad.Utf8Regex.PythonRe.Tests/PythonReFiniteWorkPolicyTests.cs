using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteWorkPolicyTests
{
    public static IEnumerable<object[]> Operations()
    {
        // Exact literal, two native-specialized forms, and general scalar discovery.
        foreach (var (pattern, input) in new[] { ("cat", "cat"), ("cat\\d+", "cat123"),
            ("(?s:cat.*dog)", "cat😀dog"), ("(?P<x>a|ab)+(?P=x)", "abab"), (".*?", "😀a") })
            for (var operation = 0; operation < 25; operation++)
                yield return [operation, pattern, input];
    }

    private static Utf8PythonRegex RegexWith(string pattern, ulong limit, bool bytesPattern = false)
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = limit };
        return bytesPattern
            ? new(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy)
            : new(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
    }

    private static string Run(Utf8PythonRegex regex, byte[] input, int operation, int start = 0,
        CancellationToken token = default) => operation switch
    {
        0 => regex.IsMatch(input, start, token).ToString(),
        1 => regex.Search(input, start, token).Success.ToString(),
        2 => regex.Match(input, start, token).Success.ToString(),
        3 => regex.FullMatch(input, start, token).Success.ToString(),
        4 => regex.Count(input, start, token).ToString(),
        5 => regex.SearchToString(input, start, token) ?? "<none>",
        6 => regex.MatchToString(input, start, token) ?? "<none>",
        7 => regex.FullMatchToString(input, start, token) ?? "<none>",
        8 => regex.SearchDetailed(input, start, token).Success.ToString(),
        9 => regex.MatchDetailed(input, start, token).Success.ToString(),
        10 => regex.FullMatchDetailed(input, start, token).Success.ToString(),
        11 => regex.SearchDetailedData(input, start, token).Value.ValueText,
        12 => regex.MatchDetailedData(input, start, token).Value.ValueText,
        13 => regex.FullMatchDetailedData(input, start, token).Value.ValueText,
        14 => string.Join("|", regex.FindAll(input, start, token).Select(x => x.ValueText)),
        15 => string.Join("|", regex.FindIterDetailed(input, start, token).Select(x => x.Value.ValueText)),
        16 => string.Join("|", regex.FindAllToStrings(input, start, token).ScalarValues),
        17 => regex.FindAllToUtf8(input, start, token).ScalarValues.Length.ToString(),
        18 => string.Join("|", regex.SplitToStrings(input, 0, start, token)),
        19 => string.Join("|", regex.SplitDetailed(input, 0, start, token).Select(x => x.ValueText)),
        20 => regex.SubnToString(input, "<\\g<0>>", 0, start, token).ResultText,
        21 => Encoding.UTF8.GetString(regex.Subn(input, "<\\g<0>>", 0, start, token).ResultBytes),
        22 => regex.SubnToString(input, "x", static (state, _) => state, 0, start, token).ResultText,
        23 => Encoding.UTF8.GetString(regex.Subn(input, "x", static (state, _) => state, 0, start, token).ResultBytes),
        24 => Encoding.UTF8.GetString(regex.Subn(input, "x"u8.ToArray(), static (state, _) => state, 0, start, token).ResultBytes),
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
        var expected = new Utf8PythonRegex(pattern).SearchDetailedData(input);
        for (ulong limit = 1; limit <= 4096; limit++)
        {
            try
            {
                var actual = RegexWith(pattern, limit).SearchDetailedData(input);
                Assert.Equal(expected.Success, actual.Success);
                Assert.Equal(expected.Groups, actual.Groups);
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
        var input = "😀a"u8.ToArray();
        ulong firstSteps;
        using (var probe = RegexWith(".", ulong.MaxValue).CreateMatchCursor(input))
        {
            Assert.True(probe.MoveNext()); firstSteps = probe.WorkingInvocation.Budget.Steps;
            Assert.True(probe.MoveNext()); Assert.True(probe.WorkingInvocation.Budget.Steps > firstSteps);
        }
        using var cursor = RegexWith(".", firstSteps).CreateMatchCursor(input);
        var alias = cursor;
        var authority = cursor.WorkingInvocation;
        Assert.True(cursor.MoveNext()); var retained = alias.Current;
        Assert.Equal(4, retained.Value.EndOffsetInBytes);
        Exhausted(() => alias.MoveNext(), firstSteps);
        Assert.Equal(0UL, authority.Ledger.ReservedBytes);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
        alias.Dispose(); cursor.Dispose();
        Assert.Equal("😀", retained.Value.ValueText);
    }

    [Fact]
    public void EmptyRetryAndSupplementaryOffsetsRemainScalar()
    {
        var regex = RegexWith(".*?", 10000);
        var all = regex.FindIterDetailed("😀a"u8);
        Assert.Equal(new[] { "", "😀", "", "a", "" }, all.Select(x => x.Value.ValueText));
        Assert.Equal(4, regex.Search("😀a"u8, 4).StartOffsetInBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Search("😀a"u8, 1));
        Assert.Equal(1, regex.Count("😀a"u8, 5));
    }

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(11)] [InlineData(14)] [InlineData(20)]
    public void ValidationCoversMalformedTailsAndWorkCoversLateMisses(int operation)
    {
        var malformed = "cat"u8.ToArray().Concat(new byte[] { 255 }).ToArray();
        Assert.Throws<ArgumentException>(() => Run(RegexWith("cat", 10000), malformed, operation));
        var miss = Encoding.UTF8.GetBytes(new string('x', 16384));
        Exhausted(() => Run(RegexWith("cat", 100), miss, operation), 100);
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

    [Fact]
    public void CallbackReturnEncodingAndCopyingConsumeTheSameCap()
    {
        var called = false;
        var regex = RegexWith("a", 100);
        Exhausted(() => regex.SubnToString("a"u8, 0, (_, _) =>
        {
            called = true; return new string('x', 65536);
        }, 0, 0), 100);
        Assert.True(called);
        called = false;
        Exhausted(() => regex.Subn("a"u8, 0, (_, _) =>
        {
            called = true; return new byte[1 << 20];
        }, 0, 0), 100);
        Assert.True(called);
    }
}

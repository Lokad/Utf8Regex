using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFullProgramReuseTests
{
    [Fact]
    public void FirstFullMatchRunsWithItsInvocationBudgetAlone()
    {
        using var regex = new Utf8PythonRegex("[A-Z]{3}[0-9]{3}", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1, MaxWorkSteps = 64 }
        });
        using var cancellation = new CancellationTokenSource();
        using var unrelated = new Utf8ConstructionScope(1, 1, 1, 1);
        Assert.True(regex.Match("ABC123"u8, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("ABC123x"u8, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal(0UL, unrelated.Budget.WorkSteps);
        Assert.Equal(0UL, unrelated.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FullMatchRetriesKeepTheOriginalInvocation(bool cursorMode, bool expire)
    {
        const string pattern = "(?P<x>a|😀)+?";
        using var regex = new Utf8PythonRegex(pattern, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var input = Enumerable.Repeat((byte)'a', 256).ToArray();
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (expire) ticks = 100;
            else cancellation.Cancel();
        });
        try
        {
            if (expire)
            {
                var error = Assert.Throws<RegexMatchTimeoutException>(Match);
                Assert.Equal(pattern, error.Pattern);
                Assert.Equal(TimeSpan.FromMilliseconds(20), error.MatchTimeout);
            }
            else
                Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(Match).CancellationToken);
        }
        finally { session.Complete(); }
        ticks = 0;
        Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);

        void Match()
        {
            if (cursorMode)
            {
                using var cursor = regex.CreateMatchCursor(input, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.FullMatch);
                cursor.MoveNext(out _);
            }
            else
                regex.Match(input, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.FullMatch);
        }
    }

    [Theory]
    [InlineData("a|ab", "ab", true)]
    [InlineData("a|ab", "abc", false)]
    [InlineData("(?>a|ab)", "ab", false)]
    [InlineData("a*?", "aaa", true)]
    [InlineData("a$", "a\n", false)]
    [InlineData("(?P<x>😀|a)+?", "😀a", true)]
    [InlineData("", "", true)]
    [InlineData(@"\w+", "éa", true)]
    [InlineData(@"\w+", "éa\n", false)]
    [InlineData(@"\w{1,3}", "aaaa", false)]
    [InlineData(@"\w{1,3}", "aaa", true)]
    [InlineData(@"(?P<x>\w+)", "éa", true)]
    [InlineData(@"(?P<x>.+)", "😀a", true)]
    [InlineData(@"(?s:.+)", "😀\n", true)]
    [InlineData(@"[\s\S]+", "😀\n", true)]
    [InlineData(@"(?<=(a))b", "b", false)]
    public void EndRequirementParticipatesInBacktracking(string pattern, string input, bool success)
    {
        var regex = new Utf8PythonRegex(pattern);
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        Assert.Equal(success, regex.Match(bytes, mode: Utf8PythonMatchMode.FullMatch).Success);
        Utf8PythonRangeMatch snapshot1 = default;
        using (var captureCursor1 = regex.CreateMatchCursor(bytes, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out snapshot1);
        }
        Assert.Equal(success, snapshot1.Success);
        Utf8PythonRangeMatch snapshot2 = default;
        using (var captureCursor2 = regex.CreateMatchCursor(bytes, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor2.MoveNext(out snapshot2);
        }
        Assert.Equal(success, snapshot2.Success);
        PythonReMatchAssert.Equal(success ? input : null, regex.Match(bytes, mode: Utf8PythonMatchMode.FullMatch));
    }
}

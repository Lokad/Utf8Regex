using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCaptureCursorInterruptionTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var (pattern, unit) in new[] { ("(?P<word>[a-z]+)", "alpha beta "),
            ("([a-z]+)([0-9]+)", "alpha12 beta34 "), ("(😀)?", "😀é"), ("x*", "xé😀y"),
            ("(([^ ]+))", "é😀 word "), ("(foo)?([a-z]+)", "bar fooword "),
            (new string('a', 63) + "([0-9]+)", new string('a', 127) + "3 ") })
            foreach (var finite in new[] { false, true })
                foreach (var expire in new[] { false, true })
                    yield return [pattern, unit, finite, expire];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryObservedCursorCheckpointKeepsOriginalAuthorityAndPriorSnapshots(
        string pattern, string unit, bool finite, bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var timeout = TimeSpan.FromMilliseconds(20);
        var regex = new Utf8PythonRegex(pattern, policy: new()
        {
            MatchTimeout = timeout,
            ResourcePolicy = finite ? new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 1024,
                MaxWorkingBytes = 1048576,
                MaxResultBytes = 1048576
            } : Utf8ResourcePolicy.Unlimited
        });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(unit, 16)));
        var checkpoints = new List<ulong>();
        var retained = new List<Utf8PythonRangeMatch>();
        var observation = Utf8SearchDiagnosticsSession.Start(steps => checkpoints.Add(steps));
        try
        {
            using var cursor = regex.CreateMatchCursor(input);
            while (cursor.MoveNext(out var match))
                retained.Add(match);
        }
        finally
        {
            observation.Complete();
        }
        Assert.NotEmpty(checkpoints);
        foreach (var stop in checkpoints.Distinct())
        {
            ticks = 0;
            using var token = new CancellationTokenSource();
            var entered = false;
            var injection = Utf8SearchDiagnosticsSession.Start(steps =>
            {
                if (steps != stop)
                    return;
                entered = true;
                if (expire)
                    ticks = 50;
                else
                    token.Cancel();
            });
            var prefix = new List<Utf8PythonRangeMatch>();
            Utf8PythonRangeMatch pending = default;
            Exception? failure = null;
            try
            {
                using var cursor = regex.CreateMatchCursor(input, cancellationToken: token.Token);
                while (cursor.MoveNext(out pending))
                {
                    prefix.Add(pending);
                    pending = default;
                }
            }
            catch (Exception error)
            {
                failure = error;
            }
            finally
            {
                injection.Complete();
            }
            Assert.True(entered);
            Assert.False(pending.Success);
            if (expire)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal(pattern, error.Pattern);
                Assert.Equal(timeout, error.MatchTimeout);
            }
            else
                Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.InRange(prefix.Count, 0, retained.Count);
            AssertSnapshots(retained.Take(prefix.Count).ToArray(), prefix);
        }
        ticks = 0;
        var fresh = new List<Utf8PythonRangeMatch>();
        using (var cursor = regex.CreateMatchCursor(input))
            while (cursor.MoveNext(out var match))
                fresh.Add(match);
        AssertSnapshots(retained, fresh);
    }

    // Assertions inspect supplied snapshots only; production cursors execute above.
    private static void AssertSnapshots(IReadOnlyList<Utf8PythonRangeMatch> expected,
        IReadOnlyList<Utf8PythonRangeMatch> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < actual.Count; index++)
        {
            Assert.Equal(expected[index].Groups.ToArray(), actual[index].Groups.ToArray());
            Assert.Equal(expected[index].LastIndex, actual[index].LastIndex);
            Assert.Equal(expected[index].NameEntries.ToArray(), actual[index].NameEntries.ToArray());
            for (var group = 0; group < actual[index].Groups.Length; group++)
                Assert.Equal(expected[index].GetGroupMemory(group).ToArray(), actual[index].GetGroupMemory(group).ToArray());
        }
    }
}

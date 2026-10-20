using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReStringFindAllInterruptionTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var (pattern, unit) in new[] { ("(?P<word>[a-z]+)", "alpha beta "),
            ("([a-z]+)([0-9]+)", "alpha12 beta34 "), ("(😀)?", "😀é") })
        foreach (var finite in new[] { false, true })
        foreach (var expire in new[] { false, true })
            yield return [pattern, unit, finite, expire];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryObservedCheckpointHonorsOriginalAuthorityBeforePublishing(
        string pattern, string unit, bool finite, bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var timeout = TimeSpan.FromMilliseconds(20);
        var policy = finite ? new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 1024,
            MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 } : Utf8ResourcePolicy.Unlimited;
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, timeout, policy);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(unit, 16)));
        var checkpoints = new List<ulong>();
        Utf8PythonFindAllResult retained;
        var observation = Utf8SearchDiagnosticsSession.Start(steps => checkpoints.Add(steps));
        try { retained = regex.FindAllToStrings(input); }
        finally { observation.Complete(); }
        Assert.NotEmpty(checkpoints);
        var expected = Flatten(retained);

        // Include the last observable checkpoint, rather than relying on a
        // historical count or only interrupting discovery's first advance.
        foreach (var stop in checkpoints)
        {
            ticks = 0;
            using var token = new CancellationTokenSource();
            var entered = false;
            var injection = Utf8SearchDiagnosticsSession.Start(steps =>
            {
                if (steps != stop) return;
                entered = true;
                if (expire) ticks = 50;
                else token.Cancel();
            });
            Utf8PythonFindAllResult? published = null;
            Exception? failure;
            try { failure = Record.Exception(() => published = regex.FindAllToStrings(input, 0, token.Token)); }
            finally { injection.Complete(); }
            Assert.True(entered);
            Assert.Null(published);
            if (expire)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal(pattern, error.Pattern);
                Assert.Equal(timeout, error.MatchTimeout);
            }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(expected, Flatten(retained));
        }
        ticks = 0;
        Assert.Equal(expected, Flatten(regex.FindAllToStrings(input)));
    }

    private static string[] Flatten(Utf8PythonFindAllResult result) =>
        result.Shape == Utf8PythonFindAllShape.GroupTuple
            ? result.TupleValues.SelectMany(tuple => tuple).ToArray() : result.ScalarValues;
}

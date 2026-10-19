using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCountPublicationTests
{
    private static T Field<T>(Utf8PythonRegex value, string name) =>
        (T)typeof(Utf8PythonRegex).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;

    [Fact]
    public void CountRequiresItsTerminalWorkAndDoesNotPublishPartialResults()
    {
        var input = "😀x"u8.ToArray();
        const string pattern = "(😀*)";
        var regex = new Utf8PythonRegex(pattern);
        var program = Field<Utf8SliceProgram>(regex, "_scalarProgram");
        var candidate = Field<global::Lokad.Utf8Regex.Utf8Regex?>(regex, "_utf8Regex");
        var invocation = new Utf8SliceInvocation(new(100000, 1024, 1048576, 1), Regex.InfiniteMatchTimeout, default);
        var cursor = new PythonReScalarMatchCursor(input, program, 0, Regex.InfiniteMatchTimeout, candidate, pattern, invocation);
        ulong work;
        try
        {
            var count = 0;
            while (cursor.MoveNext()) count++;
            Assert.Equal(3, count);
            work = cursor.WorkingInvocation.Budget.Steps;
        }
        finally { cursor.Dispose(); }
        foreach (var admit in new[] { false, true })
        {
            var bounded = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
                new() { MaxWorkSteps = work - (admit ? 0UL : 1UL), MaxDepth = 1024,
                    MaxWorkingBytes = 1048576, MaxResultBytes = 1 });
            int? published = null;
            var failure = Record.Exception(() => published = bounded.Count(input));
            if (admit) { Assert.Null(failure); Assert.Equal(3, published); }
            else
            {
                var error = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
                Assert.Null(published);
            }
        }
        Assert.Equal(3, regex.Count(input));
    }

    [Fact]
    public void CursorTerminalWorkDenialFaultsWithoutInvalidatingOwnedSnapshots()
    {
        var input = "😀x"u8.ToArray();
        const string pattern = "(😀*)";
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 1024,
            MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 };
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
        ulong work;
        var matches = 0;
        using (var measured = regex.CreateMatchCursor(input))
        {
            var observer = measured.WorkingInvocation;
            while (measured.MoveNext()) matches++;
            work = observer.Budget.Steps;
        }
        var bounded = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            policy with { MaxWorkSteps = work - 1 });
        using var cursor = bounded.CreateMatchCursor(input);
        var invocation = cursor.WorkingInvocation;
        Assert.True(invocation.Ledger.TryReserve(13));
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        for (var index = 1; index < matches; index++) Assert.True(cursor.MoveNext());
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext());
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
        cursor.Dispose();
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal("😀", retained.Value.ValueText);
        Assert.Equal("😀", retained.Groups[1].ValueText);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void TerminalCursorFailureKeepsOriginalAuthorityAndRetainedSnapshots(bool finite, bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var policy = finite ? new Utf8ResourcePolicy
        {
            MaxWorkSteps = 100000, MaxDepth = 1024, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576,
        } : Utf8ResourcePolicy.Unlimited;
        var timeout = TimeSpan.FromMilliseconds(20);
        var regex = new Utf8PythonRegex("(a*)", PythonReCompileOptions.None, timeout, policy);
        byte[] input = [];
        ulong terminal = 0;
        var expected = -1;
        for (var length = 0; length < 256 && expected < 0; length++)
        for (var tail = 0; tail < 8 && expected < 0; tail++)
        {
            input = Encoding.UTF8.GetBytes(new string('a', length) + new string('x', tail));
            using var measured = regex.CreateMatchCursor(input, 0, token.Token);
            var observer = measured.WorkingInvocation;
            var count = 0;
            while (measured.MoveNext()) count++;
            terminal = observer.Budget.Steps;
            if ((terminal & 63) == 0) expected = count;
        }
        Assert.True(expected > 0, "No bounded terminal cursor checkpoint witness found.");

        using var cursor = regex.CreateMatchCursor(input, 0, token.Token);
        var invocation = cursor.WorkingInvocation;
        Assert.True(invocation.Ledger.TryReserve(13));
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = steps =>
        {
            if (steps != terminal) return;
            entered = true;
            if (expire) ticks = 50; else token.Cancel();
        };
        Utf8PythonDetailedMatchData retained = default;
        Exception? failure = null;
        bool? exhausted = null;
        try
        {
            while (true)
            {
                bool next;
                try { next = cursor.MoveNext(); }
                catch (Exception error) { failure = error; break; }
                if (!next) { exhausted = true; break; }
                retained = cursor.Current;
            }
            Assert.True(entered);
            Assert.Null(exhausted);
            if (expire)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal("(a*)", error.Pattern); Assert.Equal(timeout, error.MatchTimeout);
                if (finite) Assert.Empty(error.Input);
            }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
        }
        finally { cursor.Dispose(); }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.True(retained.Success);
        Assert.Equal(string.Empty, retained.Value.ValueText);
        Assert.Equal(string.Empty, retained.Groups[1].ValueText);
    }

    [Theory]
    [InlineData("(a*)", false, false)] [InlineData("(a*)", true, false)]
    [InlineData("(a*)", false, true)] [InlineData("(a*)", true, true)]
    [InlineData("(?!x)a+", false, false)] [InlineData("(?!x)a+", true, false)]
    [InlineData("(?!x)a+", false, true)] [InlineData("(?!x)a+", true, true)]
    public void TerminalVmStepCannotPublishCountAfterInterruption(string pattern, bool finite, bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var limits = finite ? new Utf8ExecutionLimits(100000, 1024, 1048576, 1048576) : Utf8ExecutionLimits.Unlimited;
        var policy = finite ? new Utf8ResourcePolicy
        {
            MaxWorkSteps = 100000, MaxDepth = 1024, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576,
        } : Utf8ResourcePolicy.Unlimited;
        var timeout = TimeSpan.FromMilliseconds(20);
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, timeout, policy);
        var program = Field<Utf8SliceProgram>(regex, "_scalarProgram");
        var candidate = Field<global::Lokad.Utf8Regex.Utf8Regex?>(regex, "_utf8Regex");
        Assert.False(candidate?.CanFindNativeLiteralForResult == true);
        byte[] input = [];
        ulong terminal = 0;
        var expected = -1;
        for (var length = 0; length < 256 && expected < 0; length++)
        for (var tail = 0; tail < 8 && expected < 0; tail++)
        {
            input = Encoding.UTF8.GetBytes(new string('a', length) + new string('x', tail));
            var invocation = new Utf8SliceInvocation(limits, timeout, token.Token);
            var cursor = new PythonReScalarMatchCursor(input, program, 0, timeout, candidate, pattern, invocation);
            try
            {
                var count = 0;
                while (cursor.MoveNext()) count++;
                terminal = cursor.WorkingInvocation.Budget.Steps;
                if ((terminal & 63) == 0) expected = count;
            }
            finally { cursor.Dispose(); }
        }
        Assert.True(expected >= 0, "No bounded terminal VM checkpoint witness found.");
        var entered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps != terminal) return;
            entered = true;
            if (expire) ticks = 50; else token.Cancel();
        });
        int? published = null;
        Exception? failure;
        try { failure = Record.Exception(() => published = regex.Count(input, 0, token.Token)); }
        finally { diagnostics.Complete(); }
        Assert.True(entered);
        Assert.Null(published);
        if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        ticks = 0;
        Assert.Equal(expected, regex.Count(input));
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var (pattern, text, start, count) in new[]
        {
            ("(a*)", "", 0, 1),
            ("(a*)", "aaa", 0, 2),
            ("(?!x)a+", "aaa", 0, 1),
            ("(?!x)a+", "xx", 0, 0),
            ("(?=😀)", "é😀", 2, 1),
        })
        foreach (var finite in new[] { false, true })
        foreach (var expire in new[] { false, true })
            yield return [pattern, text, start, count, finite, expire];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void LastPublicPollCannotPublishAfterOriginalInterruption(
        string pattern, string text, int start, int expected, bool finite, bool expire)
    {
        var policy = finite ? new Utf8ResourcePolicy
        {
            MaxWorkSteps = 100000, MaxDepth = 1024, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576,
        } : Utf8ResourcePolicy.Unlimited;
        var timeout = TimeSpan.FromMilliseconds(20);
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, timeout, policy);
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();

        // Find the last clock poll in the actual public route, without pinning
        // its current numerical work requirement or adding a forwarding executor.
        var reads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => { reads++; return 0; }))
            Assert.Equal(expected, regex.Count(input, start, token.Token));
        var terminal = reads;
        Assert.True(terminal > 0);

        reads = 0;
        var entered = false;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads != terminal) return ticks;
            entered = true;
            if (!expire) token.Cancel();
            else ticks = 50;
            return ticks;
        });
        int? published = null;
        var failure = Record.Exception(() => published = regex.Count(input, start, token.Token));
        Assert.True(entered);
        Assert.Null(published);
        if (expire)
        {
            var error = Assert.IsType<RegexMatchTimeoutException>(failure);
            Assert.Equal(pattern, error.Pattern);
            Assert.Equal(timeout, error.MatchTimeout);
            if (finite) Assert.Empty(error.Input);
        }
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
    }
}

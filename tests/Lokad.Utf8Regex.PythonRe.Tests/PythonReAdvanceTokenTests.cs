using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAdvanceTokenTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedAdvanceTokenHasNoAuthorityOverLaterMoves(bool withCreationToken)
    {
        Utf8PythonRangeMatch cursorRange = default;
        using var creation = new CancellationTokenSource();
        using var firstToken = new CancellationTokenSource();
        using var secondToken = new CancellationTokenSource();
        var regex = new Utf8PythonRegex(@"(?P<word>\w+)");
        using var cursor = regex.CreateMatchCursor("é 😀two"u8.ToArray(), 0,
            withCreationToken ? creation.Token : default);
        var alias = cursor;
        Assert.True(cursor.MoveNext(out cursorRange, firstToken.Token)); var first = cursorRange;
        firstToken.Cancel();
        Assert.True(alias.MoveNext(out cursorRange, secondToken.Token));
        Assert.Equal("two", System.Text.Encoding.UTF8.GetString(cursorRange.GetValueMemory().Span));
        secondToken.Cancel();
        Assert.False(cursor.MoveNext(out cursorRange));
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        alias.Dispose(); Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _, creation.Token));
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveAdvanceCancellationInterruptsWorkAndReleasesScratch(bool withCreationToken)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        Utf8PythonRangeMatch cursorRange = default;
        using var creation = new CancellationTokenSource();
        using var operation = new CancellationTokenSource();
        var regex = new Utf8PythonRegex("(z)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 4 << 20,
                MaxResultBytes = 8 << 20
            }
        });
        var account = new PythonReSharedWorkingAllowanceTests.Allowance(4 << 20);
        Assert.True(account.TryReserve(13));
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes("z" + new string('a', 262144)), 0,
            withCreationToken ? creation.Token : default, account);
        Assert.True(cursor.MoveNext(out cursorRange)); var retained = cursorRange;
        var seed = cursor.WorkingInvocation.Budget.DiagnosticCheckpoints; var entered = false;
        cursor.WorkingInvocation.CheckpointHook = steps =>
        {
            if (steps > seed + 64) { entered = true; operation.Cancel(); }
        };
        var failure = Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _, operation.Token));
        Assert.Equal(operation.Token, failure.CancellationToken);
        Assert.True(entered);
        Assert.Equal(13UL, account.Used); account.Release(13);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
        Assert.Equal("z", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Fact]
    public void OriginalTokenKeepsPrecedenceAndAuthority()
    {
        Utf8PythonRangeMatch cursorRange = default;
        using var creation = new CancellationTokenSource();
        using var operation = new CancellationTokenSource();
        using var cursor = new Utf8PythonRegex("(a)").CreateMatchCursor("a a"u8.ToArray(), 0, creation.Token);
        Assert.True(cursor.MoveNext(out cursorRange, operation.Token)); var retained = cursorRange;
        creation.Cancel(); operation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _, operation.Token));
        Assert.Equal(creation.Token, failure.CancellationToken);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Fact]
    public void FirstMoveTokenControlsAnOtherwiseUnmeteredNativeSearch()
    {
        using var operation = new CancellationTokenSource();
        using var cursor = new Utf8PythonRegex("z").CreateMatchCursor(Encoding.UTF8.GetBytes(new string('a', 262144)));
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = steps => { entered = true; operation.Cancel(); };
        Assert.Equal(operation.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _, operation.Token)).CancellationToken);
        Assert.True(entered);
    }

    [Fact]
    public void AdvanceTokenAlsoInterruptsRequiredProjection()
    {
        using var operation = new CancellationTokenSource();
        var account = new PythonReSharedWorkingAllowanceTests.Allowance(1 << 20);
        Assert.True(account.TryReserve(13));
        var regex = new Utf8PythonRegex("(é)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 4 << 20
            }
        });
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes("é" + new string('x', 2048) + "é"),
            0, default, account);
        Assert.True(cursor.MoveNext(out var retained));
        var entered = false;
        // The VM is already prepared. Cancel at the next result-storage admission.
        account.AfterGrant = () => { entered = true; operation.Cancel(); };
        Assert.Equal(operation.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _, operation.Token)).CancellationToken);
        Assert.True(entered); Assert.Equal(13UL, account.Used); account.Release(13);
        Assert.Equal("é", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Fact]
    public void MovingWithDifferentTokensDoesNotResetWorkAdmission()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        Utf8PythonRangeMatch cursorRange = default;
        using var first = new CancellationTokenSource(); using var second = new CancellationTokenSource();
        var policy = new Utf8ResourcePolicy { MaxWorkingBytes = 1 << 20 };
        ulong secondSteps;
        using (var probe = new Utf8PythonRegex("(a)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        }).CreateMatchCursor("aa"u8.ToArray()))
        {
            Assert.True(probe.MoveNext(out _, first.Token));
            Assert.True(probe.MoveNext(out _, second.Token)); secondSteps = probe.WorkingInvocation.Budget.DiagnosticCheckpoints;
        }
        using var cursor = new Utf8PythonRegex("(a)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        }).CreateMatchCursor("aa"u8.ToArray());
        Assert.True(cursor.MoveNext(out cursorRange, first.Token)); var retained = cursorRange;
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Fact]
    public void OriginalDeadlineIsNotRenewedByAnAdvanceToken()
    {
        Utf8PythonRangeMatch cursorRange = default;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var operation = new CancellationTokenSource();
        using var cursor = new Utf8PythonRegex("(a)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(100)
        }).CreateMatchCursor("a a"u8.ToArray());
        Assert.True(cursor.MoveNext(out cursorRange, operation.Token)); var retained = cursorRange;
        ticks = 100;
        Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _, operation.Token));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Fact]
    public void NewAuthorityDoesNotBypassAliasExclusionOrEmptyProgression()
    {
        Utf8PythonRangeMatch cursorRange = default;
        Utf8PythonRangeMatch cursorRange_empty = default;
        using var operation = new CancellationTokenSource();
        using var other = new CancellationTokenSource(); other.Cancel();
        using var cursor = new Utf8PythonRegex("(z)").CreateMatchCursor(Encoding.UTF8.GetBytes(new string('a', 262144) + "z"));
        var alias = cursor; var entered = false;
        cursor.WorkingInvocation.CheckpointHook = steps =>
        {
            entered = true;
            Assert.Throws<InvalidOperationException>(() => alias.MoveNext(out _, other.Token));
        };
        Assert.True(cursor.MoveNext(out cursorRange, operation.Token)); Assert.True(entered);
        Assert.Equal("z", System.Text.Encoding.UTF8.GetString(cursorRange.GetValueMemory().Span));

        using var empty = new Utf8PythonRegex("|a").CreateMatchCursor("a"u8.ToArray());
        var values = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            using var advance = new CancellationTokenSource();
            if (!empty.MoveNext(out cursorRange_empty, advance.Token)) break;
            values.Add(System.Text.Encoding.UTF8.GetString(cursorRange_empty.GetValueMemory().Span)); advance.Cancel();
        }
        Assert.Equal(new[] { "", "a", "" }, values);
    }
}

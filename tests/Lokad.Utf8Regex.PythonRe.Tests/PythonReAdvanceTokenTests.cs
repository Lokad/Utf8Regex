using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAdvanceTokenTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompletedAdvanceTokenHasNoAuthorityOverLaterMoves(bool withCreationToken)
    {
        using var creation = new CancellationTokenSource();
        using var firstToken = new CancellationTokenSource();
        using var secondToken = new CancellationTokenSource();
        var regex = new Utf8PythonRegex(@"(?P<word>\w+)");
        using var cursor = regex.CreateMatchCursor("é 😀two"u8.ToArray(), 0,
            withCreationToken ? creation.Token : default);
        var alias = cursor;
        Assert.True(cursor.MoveNext(firstToken.Token)); var first = alias.Current;
        firstToken.Cancel();
        Assert.True(alias.MoveNext(secondToken.Token));
        Assert.Equal("two", cursor.Current.Value.ValueText);
        secondToken.Cancel();
        Assert.False(cursor.MoveNext());
        Assert.Equal("é", first.Groups[1].ValueText);
        alias.Dispose(); Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(creation.Token));
        Assert.Equal("é", first.Groups[1].ValueText);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActiveAdvanceCancellationInterruptsWorkAndReleasesScratch(bool withCreationToken)
    {
        using var creation = new CancellationTokenSource();
        using var operation = new CancellationTokenSource();
        var regex = new Utf8PythonRegex("(z)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxWorkingBytes = 4 << 20, MaxResultBytes = 8 << 20 });
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes("z" + new string('a', 262144)), 0,
            withCreationToken ? creation.Token : default);
        Assert.True(cursor.MoveNext()); var retained = cursor.Current;
        var invocation = cursor.WorkingInvocation;
        Assert.True(invocation.Ledger.TryReserve(13));
        var seed = invocation.Budget.Steps; var entered = false;
        cursor.WorkingInvocation.CheckpointHook = steps =>
        {
            if (steps > seed + 64) { entered = true; operation.Cancel(); }
        };
        var failure = Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(operation.Token));
        Assert.Equal(operation.Token, failure.CancellationToken);
        Assert.True(entered); Assert.True(invocation.Budget.Steps > seed);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
        Assert.Throws<InvalidOperationException>(() => cursor.Current);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
        Assert.Equal("z", retained.Value.ValueText);
    }

    [Fact]
    public void OriginalTokenKeepsPrecedenceAndAuthority()
    {
        using var creation = new CancellationTokenSource();
        using var operation = new CancellationTokenSource();
        using var cursor = new Utf8PythonRegex("(a)").CreateMatchCursor("a a"u8.ToArray(), 0, creation.Token);
        Assert.True(cursor.MoveNext(operation.Token)); var retained = cursor.Current;
        creation.Cancel(); operation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(operation.Token));
        Assert.Equal(creation.Token, failure.CancellationToken);
        Assert.Equal("a", retained.Value.ValueText);
    }

    [Fact]
    public void FirstMoveTokenControlsAnOtherwiseUnmeteredNativeSearch()
    {
        using var operation = new CancellationTokenSource();
        using var cursor = new Utf8PythonRegex("z")
            .CreateMatchCursor(Encoding.UTF8.GetBytes(new string('a', 262144)));
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = _ => { entered = true; operation.Cancel(); };
        Assert.Equal(operation.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(operation.Token)).CancellationToken);
        Assert.True(entered); Assert.Throws<InvalidOperationException>(() => cursor.Current);
    }

    [Fact]
    public void AdvanceTokenAlsoInterruptsRequiredProjection()
    {
        using var operation = new CancellationTokenSource();
        var regex = new Utf8PythonRegex("(é+)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxWorkingBytes = 4 << 20 });
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(new string('é', 65536)));
        var invocation = cursor.WorkingInvocation; var entered = false;
        Assert.True(invocation.Ledger.TryReserve(13));
        cursor.WorkingInvocation.CheckpointHook = _ =>
        {
            if (cursor.HasCoordinateMap) { entered = true; operation.Cancel(); }
        };
        Assert.Equal(operation.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(operation.Token)).CancellationToken);
        Assert.True(entered); Assert.Throws<InvalidOperationException>(() => cursor.Current);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
    }

    [Fact]
    public void MovingWithDifferentTokensDoesNotResetWorkAdmission()
    {
        using var first = new CancellationTokenSource(); using var second = new CancellationTokenSource();
        var policy = new Utf8ResourcePolicy { MaxWorkingBytes = 1 << 20 };
        ulong secondSteps;
        using (var probe = new Utf8PythonRegex("(a)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy)
            .CreateMatchCursor("aa"u8.ToArray()))
        {
            Assert.True(probe.MoveNext(first.Token));
            Assert.True(probe.MoveNext(second.Token)); secondSteps = probe.WorkingInvocation.Budget.Steps;
        }
        using var cursor = new Utf8PythonRegex("(a)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            policy with { MaxWorkSteps = secondSteps - 1 }).CreateMatchCursor("aa"u8.ToArray());
        Assert.True(cursor.MoveNext(first.Token)); var retained = cursor.Current;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext(second.Token));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
        Assert.Equal(secondSteps - 1, failure.Limit); Assert.Equal(secondSteps, failure.Requested);
        Assert.Equal("a", retained.Value.ValueText);
    }

    [Fact]
    public void OriginalDeadlineIsNotRenewedByAnAdvanceToken()
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var operation = new CancellationTokenSource();
        using var cursor = new Utf8PythonRegex("(a)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(100))
            .CreateMatchCursor("a a"u8.ToArray());
        Assert.True(cursor.MoveNext(operation.Token)); var retained = cursor.Current;
        ticks = 100;
        Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(operation.Token));
        Assert.Equal("a", retained.Value.ValueText);
    }

    [Fact]
    public void NewAuthorityDoesNotBypassAliasExclusionOrEmptyProgression()
    {
        using var operation = new CancellationTokenSource();
        using var other = new CancellationTokenSource(); other.Cancel();
        using var cursor = new Utf8PythonRegex("(z)").CreateMatchCursor(Encoding.UTF8.GetBytes(new string('a', 262144) + "z"));
        var alias = cursor; var entered = false;
        cursor.WorkingInvocation.CheckpointHook = _ =>
        {
            entered = true;
            Assert.Throws<InvalidOperationException>(() => alias.MoveNext(other.Token));
        };
        Assert.True(cursor.MoveNext(operation.Token)); Assert.True(entered);
        Assert.Equal("z", cursor.Current.Value.ValueText);

        using var empty = new Utf8PythonRegex("|a").CreateMatchCursor("a"u8.ToArray());
        var values = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            using var advance = new CancellationTokenSource();
            if (!empty.MoveNext(advance.Token)) break;
            values.Add(empty.Current.Value.ValueText); advance.Cancel();
        }
        Assert.Equal(new[] { "", "a", "" }, values);
    }
}

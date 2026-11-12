using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixWorkspaceTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExactStateLoansResetWithoutChangingAnotherActiveWorkspace(bool finite)
    {
        var program = PosixProgram.Compile("^([[:alpha:]]+)([0-9]+)$", default);
        var invocation = new Utf8SliceInvocation(finite ? new(1000000, 128, 1 << 20) : Utf8ExecutionLimits.Unlimited,
            Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(17));
        PosixWorkspace first = default, second = default, reused = default;
        try
        {
            first = new(program, ref invocation);
            second = new(program, ref invocation);
            var firstState = new int[first.StateLength];
            var secondState = new int[second.StateLength];
            Assert.True(first.Find("sku007"u8, firstState, 0, false, ref invocation, out _, out _));
            Assert.True(second.Find("xyz42"u8, secondState, 0, false, ref invocation, out _, out _));
            second.Release(ref invocation);
            reused = new(program, ref invocation);
            Assert.False(reused.Find("1"u8, secondState, 0, false, ref invocation, out _, out _));
            Assert.False(reused.GetCapture(secondState, 2, ref invocation, out _, out _));
            Assert.True(reused.Find("q4"u8, secondState, 0, false, ref invocation, out _, out _));
            Assert.True(reused.GetCapture(secondState, 2, ref invocation, out var start, out var end));
            Assert.Equal((1, 2), (start, end));
            Assert.True(first.GetCapture(firstState, 2, ref invocation, out start, out end));
            Assert.Equal((3, 6), (start, end));
        }
        finally
        {
            first.Release(ref invocation);
            second.Release(ref invocation); // Default and repeated release remain valid.
            reused.Release(ref invocation);
        }
        Assert.Equal(17UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void HomogeneousProofEngagesThePublicGapAndDeclinesHistoryCounterexamples()
    {
        Assert.True(PosixProgram.Compile("(a*)(a*)", default).FirstAcceptanceIsLongest);
        Assert.True(PosixProgram.Compile("((😀{0,2})(😀{1,2}))", default).FirstAcceptanceIsLongest);
        Assert.False(PosixProgram.Compile("a?(ab*)?", default).FirstAcceptanceIsLongest);
        Assert.True(PosixProgram.Compile("(a?){2,4}", default).FirstAcceptanceIsLongest);
        Assert.False(PosixProgram.Compile("(a|aa)*", default).FirstAcceptanceIsLongest);
        Assert.False(PosixProgram.Compile("(a*)(b*)", default).FirstAcceptanceIsLongest);
    }

    [Fact]
    public void FixedAlternativeProofKeepsUnequalNestedAndConsumingTailsGeneral()
    {
        Assert.True(PosixProgram.Compile("(a)|(a)", default).FirstAcceptanceIsLongest);
        Assert.True(PosixProgram.Compile("((é😀))|(é(😀))", default).FirstAcceptanceIsLongest);
        Assert.False(PosixProgram.Compile("(a)|(aa)", default).FirstAcceptanceIsLongest);
        Assert.False(PosixProgram.Compile("()|(a)", default).FirstAcceptanceIsLongest);
        Assert.False(PosixProgram.Compile("(a)|(a)|(a)", default).FirstAcceptanceIsLongest);
        Assert.False(PosixProgram.Compile("((a)|(a))b*", default).FirstAcceptanceIsLongest);
    }

    [Theory]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkSteps, "(a|aa)*")]
    [InlineData(Utf8ResourceLimitKind.ExecutionDepth, "(a|aa)*")]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkingBytes, "(a|aa)*")]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkSteps, "(a?){2,4}")]
    [InlineData(Utf8ResourceLimitKind.ExecutionDepth, "(a?){2,4}")]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkingBytes, "(a?){2,4}")]
    public void ExecutionRefusalReleasesCoveredStorage(Utf8ResourceLimitKind kind, string pattern)
    {
        var program = PosixProgram.Compile(pattern, default);
        var limits = new Utf8ExecutionLimits(kind == Utf8ResourceLimitKind.ExecutionWorkSteps ? 30UL : 1_000_000,
            kind == Utf8ResourceLimitKind.ExecutionDepth ? 1U : 100,
            kind == Utf8ResourceLimitKind.ExecutionWorkingBytes ? 1UL : 1 << 20, 1);
        var account = new Account(1 << 20);
        var invocation = new Utf8SliceInvocation(limits, Timeout.InfiniteTimeSpan, default, account);
        PosixWorkspace workspace = default;
        try
        {
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                workspace = new PosixWorkspace(program, ref invocation);
                workspace.Find("aaaa"u8, new int[workspace.StateLength], 0, false, ref invocation, out _, out _);
            });
            Assert.Equal(kind, error.ResourceKind);
        }
        finally { workspace.Release(ref invocation); }
        Assert.Equal(0UL, account.Used); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void SharedRefusalAndCancellationAtAcquisitionRefundExactly()
    {
        var program = PosixProgram.Compile("(a|aa)*", default);
        var refused = new Account(0);
        var denied = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default, refused);
        Assert.Throws<Utf8MemoryAdmissionException>(() => new PosixWorkspace(program, ref denied));
        Assert.Equal(0UL, refused.Used);
        using var canceled = new CancellationTokenSource();
        var account = new Account(1 << 20) { AfterGrant = canceled.Cancel };
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, canceled.Token, account);
        var error = Assert.Throws<OperationCanceledException>(() => new PosixWorkspace(program, ref invocation));
        Assert.Equal(canceled.Token, error.CancellationToken);
        Assert.Equal(0UL, account.Used); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(63, Utf8InputValidation.Validate)] [InlineData(64, Utf8InputValidation.Validate)]
    [InlineData(65, Utf8InputValidation.Validate)]
    [InlineData(63, Utf8InputValidation.AssumeValid)] [InlineData(64, Utf8InputValidation.AssumeValid)]
    [InlineData(65, Utf8InputValidation.AssumeValid)]
    public void StateStorageBoundariesKeepCapturesAndCursorOwnership(int groups, Utf8InputValidation validation)
    {
        var pattern = new string('(', groups) + "é😀" + new string(')', groups);
        var regex = new Utf8PosixRegex(pattern, inputValidation: validation);
        var program = PosixProgram.Compile(pattern, default);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        var workspace = new PosixWorkspace(program, ref invocation);
        try { Assert.Equal(2 * groups, workspace.StateLength); }
        finally { workspace.Release(ref invocation); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        var input = "πé😀!"u8.ToArray();
        var output = new Utf8ValueMatch[groups + 1];
        var match = regex.Match(input, output);
        Assert.Equal((2, 6, 1, 3), (match.IndexInBytes, match.LengthInBytes, match.IndexInUtf16, match.LengthInUtf16));
        Assert.All(output, group => Assert.Equal(match, group));
        Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
        using var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        Assert.True(alias.MoveNext(output, out var fromCursor));
        Assert.Equal(match, fromCursor);
        var retained = output[^1];
        alias.Dispose();
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
        Assert.Equal(match, retained);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AmbiguousWholeBoundaryRetainsOriginalInterruptionAndParentLoans(bool timeout)
    {
        var program = PosixProgram.Compile("((a|aa)*).", default);
        var input = Encoding.ASCII.GetBytes(new string('a', 32) + "b!");
        using var source = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var fired = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (steps < 512) return;
            fired = true;
            if (timeout) ticks = 25; else source.Cancel();
        });
        var account = new Account(1 << 20);
        Assert.True(account.TryReserve(17));
        var invocation = new Utf8SliceInvocation(new(100_000, 4096, 1 << 20), TimeSpan.FromMilliseconds(20), source.Token, account);
        PosixWorkspace workspace = default;
        try
        {
            workspace = new PosixWorkspace(program, ref invocation);
            var state = new int[workspace.StateLength];
            var error = Record.Exception(() => { workspace.Find(input, state, 0, false, ref invocation, out _, out _); });
            Assert.True(fired);
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
            else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        }
        finally { workspace.Release(ref invocation); session.Complete(); }
        Assert.Equal(17UL, account.Used); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        account.Release(17);
    }

    private sealed class Account(ulong limit) : IUtf8MemoryAllowance
    {
        internal ulong Used;
        internal Action? AfterGrant;
        public bool TryReserve(ulong bytes)
        {
            if (bytes > limit - Used) return false;
            Used += bytes; AfterGrant?.Invoke(); return true;
        }
        public void Release(ulong bytes)
        { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}

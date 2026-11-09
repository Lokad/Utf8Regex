using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixWorkspaceTests
{
    [Fact]
    public void HomogeneousProofEngagesThePublicGapAndDeclinesHistoryCounterexamples()
    {
        Assert.True(PosixProgram.Compile("(a*)(a*)", default).FirstAcceptanceIsLongest);
        Assert.True(PosixProgram.Compile("((😀{0,2})(😀{1,2}))", default).FirstAcceptanceIsLongest);
        Assert.False(PosixProgram.Compile("a?(ab*)?", default).FirstAcceptanceIsLongest);
        Assert.False(PosixProgram.Compile("(a?){2,4}", default).FirstAcceptanceIsLongest);
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
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkSteps)]
    [InlineData(Utf8ResourceLimitKind.ExecutionDepth)]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkingBytes)]
    public void ExecutionRefusalReleasesCoveredStorage(Utf8ResourceLimitKind kind)
    {
        var program = PosixProgram.Compile("(a|aa)*", default);
        var limits = new Utf8ExecutionLimits(kind == Utf8ResourceLimitKind.ExecutionWorkSteps ? 30UL : 1_000_000,
            kind == Utf8ResourceLimitKind.ExecutionDepth ? 1U : 100,
            kind == Utf8ResourceLimitKind.ExecutionWorkingBytes ? 1UL : 1 << 20, 1);
        var account = new Account(1 << 20);
        var invocation = new Utf8SliceInvocation(limits, Timeout.InfiniteTimeSpan, default, account);
        PosixWorkspace? workspace = null;
        try
        {
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                workspace = new PosixWorkspace(program, ref invocation);
                workspace.Find("aaaa"u8, 0, false, ref invocation, out _, out _);
            });
            Assert.Equal(kind, error.ResourceKind);
        }
        finally { workspace?.Release(ref invocation); }
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

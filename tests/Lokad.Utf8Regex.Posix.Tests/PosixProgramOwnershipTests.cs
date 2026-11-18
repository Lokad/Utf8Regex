using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixProgramOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PolicyFundingSurvivesCloseUntilCursorAliasesFinish(bool factory)
    {
        var account = new Account();
        var policy = new Utf8RegexPolicy
        {
            ProgramMemoryAllowance = account,
            ResourcePolicy = new() { MaxWorkSteps = 100000, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 }
        };
        using var regex = factory ? Utf8PosixRegex.Compile("(a|aa)", policy: policy) : new Utf8PosixRegex("(a|aa)", policy: policy);
        Assert.True(account.Used > 0);
        using var cursor = regex.CreateMatchCursor("aa aa"u8.ToArray());
        var alias = cursor;
        regex.Dispose();
        Assert.True(account.Used > 0);
        Assert.Throws<ObjectDisposedException>(() => regex.IsMatch("aa"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.Match("aa"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.Count("aa"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.CreateMatchCursor("aa"u8.ToArray()));
        var groups = new Utf8CaptureRange[3]; groups[2] = new(99, 100);
        Assert.True(alias.MoveNext(out var first, groups));
        Assert.Equal(new Utf8CaptureRange(0, 2), first);
        Assert.Equal(first, groups[1]); var retained = groups[1];
        Assert.True(cursor.MoveNext(out var second, groups));
        Assert.Equal(new Utf8CaptureRange(3, 5), second);
        Assert.False(alias.MoveNext(out _, groups));
        Assert.Equal(0UL, account.Used);
        Assert.Equal(new Utf8CaptureRange(99, 100), groups[2]);
        Assert.Equal(new Utf8CaptureRange(0, 2), retained);
        Assert.Equal(1, regex.GroupCount);
        alias.Dispose(); regex.Dispose();
        Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void ClosingDuringAnAdmittedSynchronousCallDoesNotRefundItsProgramEarly()
    {
        var account = new Account();
        using var regex = Utf8PosixRegex.Compile("(a|aa)", policy: new() { ProgramMemoryAllowance = account });
        var held = account.Used; Assert.True(held > 0);
        var closed = false;
        account.AfterGrant = () =>
        {
            account.AfterGrant = null;
            regex.Dispose(); closed = true;
            Assert.True(account.Used >= held);
        };
        var groups = new Utf8CaptureRange[2];
        Assert.Equal(new Utf8CaptureRange(0, 2), regex.Match("aa"u8, captures: groups));
        Assert.True(closed); Assert.Equal(groups[0], groups[1]);
        Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void SeparateWorkingRefusalIsTerminalAndLeavesProgramFunded()
    {
        var program = new Account(); var working = new Account();
        using var regex = new Utf8PosixRegex("(a|aa)", policy: new() { ProgramMemoryAllowance = program });
        var held = program.Used;
        var groups = new Utf8CaptureRange[3]; Array.Fill(groups, new(99, 100));
        Assert.Equal(new Utf8CaptureRange(0, 2), regex.Match("aa"u8, captures: groups, workingMemoryAllowance: working));
        Assert.Equal(0UL, working.Used); Assert.Equal(held, program.Used);
        working.Maximum = 0; var attempts = working.Attempts;
        Assert.Throws<Utf8MemoryAdmissionException>(() => regex.Match("aa"u8, captures: groups, workingMemoryAllowance: working));
        Assert.Equal(attempts + 1, working.Attempts);
        Assert.Equal(held, program.Used); Assert.Equal(0UL, working.Used);
        Assert.False(groups[0].Success); Assert.False(groups[1].Success);
        Assert.Equal(new Utf8CaptureRange(99, 100), groups[2]);
    }

    [Fact]
    public void ConstructionCancellationEndsAtPublicationAndPreCancellationWinsAfterClose()
    {
        using var source = new CancellationTokenSource();
        var account = new Account();
        using var regex = Utf8PosixRegex.Compile("(a|aa)", policy: new() { ProgramMemoryAllowance = account }, cancellationToken: source.Token);
        source.Cancel();
        Assert.Equal(1, regex.Count("aa"u8));
        regex.Dispose();
        var groups = new[] { new Utf8CaptureRange(99, 100) };
        var error = Assert.Throws<OperationCanceledException>(() => regex.Match("aa"u8, -1, groups, source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(new Utf8CaptureRange(99, 100), groups[0]); Assert.Equal(0UL, account.Used);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructionRefusalRefundsAdmission(bool factory)
    {
        var account = new Account { Maximum = 0 };
        var policy = new Utf8RegexPolicy { ProgramMemoryAllowance = account };
        Assert.Throws<Utf8MemoryAdmissionException>(() =>
        {
            if (factory) _ = Utf8PosixRegex.Compile("(a|aa)+", policy: policy);
            else _ = new Utf8PosixRegex("(a|aa)+", policy: policy);
        });
        Assert.Equal(0UL, account.Used); Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void FactoryCancellationDuringAdmissionRefundsAndKeepsOriginalToken()
    {
        using var source = new CancellationTokenSource();
        var account = new Account { AfterGrant = source.Cancel };
        var error = Assert.Throws<OperationCanceledException>(() => Utf8PosixRegex.Compile("(a|aa)+",
            policy: new() { ProgramMemoryAllowance = account }, cancellationToken: source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(0UL, account.Used); Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void InvalidFundedPatternRefundsAdmission()
    {
        var account = new Account();
        Assert.Throws<ArgumentException>(() => new Utf8PosixRegex("(", policy: new() { ProgramMemoryAllowance = account }));
        Assert.Equal(0UL, account.Used); Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosedProgramCursorKeepsOriginalInterruptionAndRefundsOnFailure(bool deadline)
    {
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        using var source = new CancellationTokenSource();
        var account = new Account();
        using var regex = new Utf8PosixRegex("(a|aa)", policy: new()
        {
            ProgramMemoryAllowance = account,
            MatchTimeout = deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan
        });
        using var cursor = regex.CreateMatchCursor("aa aa"u8.ToArray(), cancellationToken: source.Token);
        regex.Dispose();
        Assert.True(cursor.MoveNext(out var retained));
        Assert.True(account.Used > 0);
        if (deadline) now = 101; else source.Cancel();
        var captures = new[] { new Utf8CaptureRange(99, 100), new Utf8CaptureRange(99, 100) };
        if (deadline) Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _, captures));
        else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _, captures)).CancellationToken);
        Assert.Equal(0UL, account.Used);
        Assert.All(captures, value => Assert.False(value.Success));
        Assert.Equal(new Utf8CaptureRange(0, 2), retained);
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
    }

    [Fact]
    public void EarlyCursorDisposalRefundsClosedProgramAndWorkspaceOnce()
    {
        var account = new Account();
        using var regex = new Utf8PosixRegex("(a|aa)", policy: new() { ProgramMemoryAllowance = account });
        using var cursor = regex.CreateMatchCursor("aa aa"u8.ToArray());
        var alias = cursor;
        regex.Dispose();
        Assert.True(cursor.MoveNext(out var retained));
        Assert.True(account.Used > 0);
        alias.Dispose(); cursor.Dispose();
        Assert.Equal(0UL, account.Used);
        Assert.Equal(new Utf8CaptureRange(0, 2), retained);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ConstructorPolicyEnforcesEachConstructionBound(int kind)
    {
        var limits = kind switch
        {
            0 => new Utf8ConstructionPolicy { MaxWorkSteps = 1 },
            1 => new Utf8ConstructionPolicy { MaxDepth = 1 },
            2 => new Utf8ConstructionPolicy { MaxInstructionCapacity = 1 },
            _ => new Utf8ConstructionPolicy { MaxBytes = 1 }
        };
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            new Utf8PosixRegex("((a|aa)*)b", policy: new() { ConstructionPolicy = limits }));
        Assert.Equal(kind switch
        {
            0 => Utf8ResourceLimitKind.ConstructionWorkSteps,
            1 => Utf8ResourceLimitKind.ConstructionDepth,
            2 => Utf8ResourceLimitKind.ConstructionInstructionCapacity,
            _ => Utf8ResourceLimitKind.ConstructionBytes
        }, error.ResourceKind);
        Assert.Equal(1UL, error.Limit); Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void ExplicitScopeCannotReplaceParentButDefaultConstructorInherits()
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var regex = new Utf8PosixRegex("(a|aa)");
        var spent = owner.Budget.WorkSteps; Assert.True(spent > 0);
        Assert.Throws<InvalidOperationException>(() => Utf8PosixRegex.Compile("a"));
        Assert.Throws<InvalidOperationException>(() => new Utf8PosixRegex("a", policy: new() { ConstructionPolicy = new() { MaxBytes = 10000 } }));
        Assert.Equal(spent, owner.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructionDeadlineIsIndependentOfMatching(bool factory)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
        var policy = new Utf8RegexPolicy { MatchTimeout = TimeSpan.FromSeconds(30), ConstructionTimeout = TimeSpan.FromMilliseconds(1) };
        var error = Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            if (factory) _ = Utf8PosixRegex.Compile("(a|aa)", policy: policy);
            else _ = new Utf8PosixRegex("(a|aa)", policy: policy);
        });
        Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used, Maximum = 16 << 20;
        internal int Attempts;
        internal Action? AfterGrant;
        public bool TryReserve(ulong bytes)
        {
            Attempts++;
            if (bytes > Maximum - Used) return false;
            Used += bytes; AfterGrant?.Invoke(); return true;
        }
        public void Release(ulong bytes) { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}

using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixWorkspaceTests
{
    [Fact]
    public void OnlyExistenceUsesTheLiteralBranchTier()
    {
        const string pattern = "ab|abc";
        var prefix = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("axé😀 ", 128)));
        byte[] input = [.. prefix, .. "abc abc"u8];
        using var regex = new Utf8PosixRegex(pattern);
        var program = regex.ScalarProgram;
        var dynamicCode = RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled;
        Assert.Equal(dynamicCode, program.ObservesProofWork);

        using var source = new CancellationTokenSource();
        for (var i = 0; i < 512; i++)
        {
            Assert.True(regex.IsMatch(input, source.Token));
            Assert.Equal(2, regex.Count(input));
        }
        Assert.Null(program.TieredProof);
        for (var i = 0; i < 8192 && program.ObservesProofWork; i++)
            Assert.True(regex.IsMatch(input));
        Assert.Equal(dynamicCode, program.TieredProof is not null);
        Assert.False(regex.IsMatch(prefix));
        Assert.Equal(new Utf8CaptureRange(prefix.Length, prefix.Length + 3), regex.Match(input));
        Assert.Equal(2, regex.Count(input));

        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, source.Token)).CancellationToken);
        using var bounded = new Utf8PosixRegex(pattern, policy: new() { ResourcePolicy = new() { MaxWorkSteps = 1 } });
        Assert.False(bounded.ScalarProgram.ObservesProofWork);
        Assert.Throws<Utf8ResourceLimitExceededException>(() => bounded.IsMatch(input));
        using var anchor = new Utf8PosixRegex("(ab|abc)$");
        Assert.False(anchor.ScalarProgram.ObservesProofWork);
        Assert.False(anchor.IsMatch("abc\n"u8));
    }

    [Fact]
    public void DirectRunsWithPrivateTagsAdmitOnlyPublicCaptureSlots()
    {
        const int prefixLength = 72;
        var pattern = "^" + string.Concat(Enumerable.Repeat("[0-9]", prefixLength)) + "([a-z]+)([0-9]+)$";
        var input = Encoding.UTF8.GetBytes(new string('1', prefixLength) + "sku007");
        var workingBytes = Utf8ConstructionBudget.ArrayBytes<int>(4);
        using var regex = new Utf8PosixRegex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = workingBytes }
        });
        var account = new Account(workingBytes);
        Span<Utf8CaptureRange> output = stackalloc Utf8CaptureRange[3];

        Assert.Equal(new Utf8CaptureRange(0, input.Length), regex.Match(input, captures: output, workingMemoryAllowance: account));
        Assert.Equal(new Utf8CaptureRange(prefixLength, prefixLength + 3), output[1]);
        Assert.Equal(new Utf8CaptureRange(prefixLength + 3, input.Length), output[2]);
        Assert.Equal(0UL, account.Used);

        input[prefixLength / 2] = (byte)'x';
        Assert.False(regex.Match(input, captures: output, workingMemoryAllowance: account).Success);
        Assert.False(output[0].Success);
        Assert.False(output[1].Success);
        Assert.False(output[2].Success);
        Assert.Equal(0UL, account.Used);
    }

    [Theory]
    [InlineData("()", "")]
    [InlineData("(.)", "😀")]
    [InlineData("([é中])", "é")]
    [InlineData("^(aé.)$", "aé😀")]
    [InlineData("^([[:alpha:]])$", "a")]
    public void FixedCaptureSequencesFitSmallWorkingBudget(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Utf8CaptureRange(0, 0 + (input.Length));
        {
            var regex = new Utf8PosixRegex(pattern, policy: new Utf8RegexPolicy()
            {
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 10_000,
                    MaxDepth = 64,
                    MaxWorkingBytes = 1024,
                    MaxResultBytes = 1
                }
            });
            var output = new Utf8CaptureRange[2];
            var account = new Account(1024); Assert.True(account.TryReserve(17));
            Assert.Equal(expected, regex.Match(input, captures: output, workingMemoryAllowance: account));
            Assert.Equal(expected, output[0]); Assert.Equal(expected, output[1]);
            Assert.Equal(17UL, account.Used); account.Release(17);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReusedStateLoansDoNotChangeAnotherActiveWorkspace(bool finite)
    {
        var program = PosixProgram.Compile("^([[:alpha:]]+)([0-9]+)$", default);
        var invocation = new Utf8SliceInvocation(finite ? new(1000000, 128, 1 << 20) : Utf8ExecutionLimits.Unlimited,
            Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(17));
        PosixWorkspace first = default, second = default, reused = default;
        try
        {
            PosixWorkspace.Initialize(out first, program, ref invocation);
            PosixWorkspace.Initialize(out second, program, ref invocation);
            var firstState = new int[first.StateLength];
            var secondState = new int[second.StateLength];
            Assert.True(first.Find("sku007"u8, firstState, 0, false, ref invocation, out _, out _));
            Assert.True(second.Find("xyz42"u8, secondState, 0, false, ref invocation, out _, out _));
            second.Release(ref invocation);
            PosixWorkspace.Initialize(out reused, program, ref invocation);
            Assert.False(reused.Find("1"u8, secondState, 0, false, ref invocation, out var missStart, out var missEnd));
            Assert.Equal((-1, -1), (missStart, missEnd));
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
    [Theory]
    [InlineData("^Z+(ab|a)$", "ZZab", 43UL)]
    [InlineData("^Z+(a|ab)$", "ZZab", 43UL)]
    [InlineData("^Z+((😀a)|(😀))$", "ZZ😀a", 45UL)]
    [InlineData("^Z+((😀)|(😀a))$", "ZZ😀a", 45UL)]
    public void TerminalByteBoundsDoNotOverrideLongerFirstChoice(string pattern, string subject, ulong maxWorkSteps)
    {
        using var regex = new Utf8PosixRegex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = maxWorkSteps, MaxDepth = 64, MaxWorkingBytes = 4096 }
        });
        using var source = new CancellationTokenSource();
        var account = new Account(4096);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(subject), cancellationToken: source.Token, workingMemoryAllowance: account));
        Assert.Equal(0UL, account.Used);
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
                PosixWorkspace.Initialize(out workspace, program, ref invocation);
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
        Assert.Throws<Utf8MemoryAdmissionException>(() => PosixWorkspace.Initialize(out _, program, ref denied));
        Assert.Equal(0UL, refused.Used);
        using var canceled = new CancellationTokenSource();
        var account = new Account(1 << 20) { AfterGrant = canceled.Cancel };
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, canceled.Token, account);
        var error = Assert.Throws<OperationCanceledException>(() => PosixWorkspace.Initialize(out _, program, ref invocation));
        Assert.Equal(canceled.Token, error.CancellationToken);
        Assert.Equal(0UL, account.Used); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }
    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    public void StateStorageBoundariesKeepCapturesAndCursorOwnership(int groups)
    {
        var pattern = new string('(', groups) + "é😀" + new string(')', groups);
        var regex = new Utf8PosixRegex(pattern);
        var program = PosixProgram.Compile(pattern, default);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        PosixWorkspace.Initialize(out var workspace, program, ref invocation);
        try { Assert.Equal(2 * groups, workspace.StateLength); }
        finally { workspace.Release(ref invocation); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        var input = "πé😀!"u8.ToArray();
        var output = new Utf8CaptureRange[groups + 1];
        var match = regex.Match(input, captures: output);
        Assert.Equal((2, 6), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        Assert.All(output, group => Assert.Equal(match, group));
        Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
        using var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        Assert.True(alias.MoveNext(captures: output, match: out var fromCursor));
        Assert.Equal(match, fromCursor);
        var retained = output[^1];
        alias.Dispose();
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(match: out _));
        Assert.Equal(match, retained);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AmbiguousWholeBoundaryRetainsOriginalInterruptionAndParentLoans(bool timeout)
    {
        var program = PosixProgram.Compile("((a|aa)*).", default);
        var input = Encoding.ASCII.GetBytes(new string('a', 32) + "b!");
        using var source = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var fired = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
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
            PosixWorkspace.Initialize(out workspace, program, ref invocation);
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

    [Theory]
    [InlineData("^([a-z]+)([0-9]+)$", false)]
    [InlineData("^([a-z]+)([0-9]+)$", true)]
    [InlineData("(a*)(a*)", false)]
    [InlineData("(a*)(a*)", true)]
    public void UniqueClassRunRetainsOriginalInterruptionAndParentLoans(string pattern, bool timeout)
    {
        var regex = new Utf8PosixRegex(pattern, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100_000,
                MaxDepth = 1,
                MaxWorkingBytes = 1 << 20
            }
        });
        var input = Encoding.ASCII.GetBytes(new string('a', 8192) + "0");
        var output = new Utf8CaptureRange[3];
        using var source = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        ulong lastCheckpoint = 0;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            lastCheckpoint = steps;
            if (timeout) ticks = 25; else source.Cancel();
        });
        var account = new Account(1 << 20);
        Assert.True(account.TryReserve(17));
        try
        {
            var error = Record.Exception(() => regex.Match(input, captures: output, cancellationToken: source.Token, workingMemoryAllowance: account));
            Assert.Equal(64UL, lastCheckpoint);
            if (timeout) Assert.IsType<System.Text.RegularExpressions.RegexMatchTimeoutException>(error);
            else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
            Assert.All(output, value => Assert.False(value.Success));
        }
        finally { session.Complete(); }
        Assert.Equal(17UL, account.Used); account.Release(17);
    }

    [Fact]
    public void UniqueClassRunsKeepWorkAdmissionWithoutImpossiblePendingDepth()
    {
        const string pattern = "^([a-z]+)([0-9]+)$";
        var input = Encoding.ASCII.GetBytes(new string('a', 4096) + "000");
        var admitted = new Utf8PosixRegex(pattern, policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100_000,
                MaxDepth = 1,
                MaxWorkingBytes = 1024,
                MaxResultBytes = 1
            }
        });
        var output = new Utf8CaptureRange[3];
        Assert.Equal(input.Length, admitted.Match(input, captures: output).EndOffsetInBytes);
        Assert.Equal(4096, (output[1].EndOffsetInBytes - output[1].StartOffsetInBytes)); Assert.Equal(3, (output[2].EndOffsetInBytes - output[2].StartOffsetInBytes));
        var refused = new Utf8PosixRegex(pattern, policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 64,
                MaxDepth = 1,
                MaxWorkingBytes = 1024,
                MaxResultBytes = 1
            }
        });
        var account = new Account(1024); Assert.True(account.TryReserve(17));
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            refused.Match(input, captures: output, workingMemoryAllowance: account));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.All(output, value => Assert.False(value.Success));
        Assert.Equal(17UL, account.Used); account.Release(17);
    }

    [Theory]
    [InlineData("([a-z]+)([0-9]+)", "😀ab12!cd34?", 2)]
    [InlineData("(([a-z]+))([0-9]+)", "😀ab12!cd34?", 2)]
    [InlineData("([a-z]+)([0-9]+)([!]+)", "😀ab12!cd34!!", 2)]
    [InlineData("([a-z]+)[0-9]+", "😀ab12!cd34?", 2)]
    [InlineData("(([a-z]+))[0-9]+", "😀ab12!cd34?", 2)]
    [InlineData("([a-z]+)[0-9]+([!]+)", "😀ab12!cd34!!", 2)]
    public void UniqueRunCapturePositionsFitStateOnlyAdmissionAndSurviveCursorClosure(string pattern, string text, int count)
    {
        var regex = new Utf8PosixRegex(pattern, policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 128,
                MaxResultBytes = 1,
                MaxDepth = 1,
                MaxWorkSteps = 10000
            }
        });
        var account = new Account(128); Assert.True(account.TryReserve(17));
        var input = Encoding.UTF8.GetBytes(text);
        var output = new Utf8CaptureRange[regex.GroupCount + 2];
        var sentinel = new Utf8CaptureRange(30, 30 + 40);
        output[^1] = sentinel;
        using var cursor = regex.CreateMatchCursor(input, workingMemoryAllowance: account);
        var alias = cursor;
        Assert.True(cursor.MoveNext(captures: output, match: out var whole));
        var retained = output.ToArray();
        Assert.Equal(4, whole.StartOffsetInBytes);
        Assert.Equal(whole, output[0]); Assert.Equal(sentinel, output[^1]);
        for (var found = 1; found < count; found++) Assert.True(alias.MoveNext(captures: output, match: out _));
        Assert.False(alias.MoveNext(captures: output, match: out _));
        Assert.All(output[..^1], value => Assert.False(value.Success));
        Assert.Equal(sentinel, output[^1]); Assert.Equal(17UL, account.Used);
        cursor.Dispose(); alias.Dispose(); Assert.Equal(17UL, account.Used); account.Release(17);
        Assert.Equal(4, retained[1].StartOffsetInBytes); Assert.Equal(2, (retained[1].EndOffsetInBytes - retained[1].StartOffsetInBytes));
        Assert.Throws<ObjectDisposedException>(() => alias.MoveNext(match: out _));
    }

    [Theory]
    [InlineData("([a-z]+)[0-9]+")]
    [InlineData("a*([a-b]*)")]
    [InlineData("(a*)(a*)")]
    public void RunBodiesKeepPublicWorkRefusalAndOriginalCancellation(string pattern)
    {
        var regex = new Utf8PosixRegex(pattern, policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 64,
                MaxDepth = 1,
                MaxWorkingBytes = 1024
            }
        });
        var input = Encoding.UTF8.GetBytes(new string('a', 8192) + "12");
        var sentinel = new Utf8CaptureRange(3, 3 + 4);
        var output = Enumerable.Repeat(sentinel, 2).ToArray();
        var account = new Account(1024); Assert.True(account.TryReserve(17));
        var refused = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            regex.Match(input, captures: output, workingMemoryAllowance: account));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, refused.ResourceKind);
        Assert.All(output, value => Assert.False(value.Success));
        Assert.Equal(17UL, account.Used);
        refused = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            regex.Count(input, workingMemoryAllowance: account));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, refused.ResourceKind);
        Assert.Equal(17UL, account.Used); account.Release(17);

        regex = new Utf8PosixRegex(pattern);
        var denied = new Account(0);
        Assert.Throws<Utf8MemoryAdmissionException>(() =>
            regex.Match("ab12"u8, captures: output, workingMemoryAllowance: denied));
        Assert.Equal(0UL, denied.Used);
        using var canceled = new CancellationTokenSource();
        account = new Account(1024) { AfterGrant = canceled.Cancel };
        var error = Assert.Throws<OperationCanceledException>(() =>
            regex.Match("ab12"u8, captures: output, cancellationToken: canceled.Token, workingMemoryAllowance: account));
        Assert.Equal(canceled.Token, error.CancellationToken);
        Assert.Equal(0UL, account.Used);
        Assert.All(output, value => Assert.False(value.Success));
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

using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FundedProgramOwnerTests
{
    private static Utf8RegexProgramOwner Compile(string pattern, Account account, RegexOptions options = RegexOptions.None,
        Utf8ResourcePolicy policy = default, CancellationToken token = default) => Utf8Regex.Compile(pattern,
            options, Timeout.InfiniteTimeSpan, policy, default, Timeout.InfiniteTimeSpan, token, account);

    [Fact]
    public void SeparateProgramAndWorkingChildrenShareOneRootAndRefundDeterministically()
    {
        var root = new SharedRoot(); var programAccount = new Account(root); var working = new Account(root);
        Assert.True(programAccount.TryReserve(13));
        using var constructionToken = new CancellationTokenSource();
        var program = Compile(@"(?<=foo)([a-z]+)-([0-9]+)", programAccount, token: constructionToken.Token);
        var held = programAccount.Used; Assert.True(held > 13); constructionToken.Cancel();
        var input = "fooabc-123 foofoo-45"u8.ToArray();
        Assert.Equal(2, program.Count(input, workingMemoryAllowance: working)); Assert.True(program.IsMatch(input, workingMemoryAllowance: working));
        Assert.Equal(0UL, working.Used); Assert.Equal(held, root.Used);
        Assert.Equal(2, program.Count(input)); Assert.Equal(held, programAccount.Used);
        program.Dispose(); program.Dispose(); Assert.Equal(13UL, root.Used);
        Assert.Throws<ObjectDisposedException>(() => program.Count(input));
        Assert.Throws<ObjectDisposedException>(() => program.IsMatch(input, workingMemoryAllowance: working));
        programAccount.Release(13); Assert.Equal(0UL, root.Used);
    }

    [Theory]
    [InlineData("alpha", "😀alpha alpha", RegexOptions.None, 2)]
    [InlineData(@"\p{Sm}", "∑𝛁+😀", RegexOptions.Compiled, 3)]
    [InlineData(@"\b\w{2,}\b", "ab cd e", RegexOptions.None, 2)]
    [InlineData(@"\b\w{2,}\b", "éé 😀 ab", RegexOptions.None, 2)]
    [InlineData(".", "é😀", RegexOptions.None, 2)]
    [InlineData("(a|aa)", "aa", RegexOptions.RightToLeft, 2)]
    [InlineData("(a|aa)", "aa", RegexOptions.NonBacktracking, 2)]
    [InlineData("a*", "ba", RegexOptions.None, 3)]
    public void WorkingAndFundedRoutesPreserveScalarAndExecutionContracts(string pattern, string subject, RegexOptions options, int count)
    {
        var root = new SharedRoot(); var programs = new Account(root); var working = new Account(root);
        var regex = new Utf8Regex(pattern, options); var bytes = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(count, regex.Count(bytes, default, working)); Assert.True(regex.IsMatch(bytes, default, working));
        Assert.Equal(0UL, root.Used);
        using (var owner = Compile(pattern, programs, options))
        {
            var held = programs.Used;
            Assert.Equal(count, owner.Count(bytes, workingMemoryAllowance: working)); Assert.True(owner.IsMatch(bytes, workingMemoryAllowance: working));
            Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
        }
        Assert.Equal(0UL, root.Used);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void DenialIsTerminalAndExistingProgramFundingSurvives(bool exists)
    {
        var root = new SharedRoot(); var programs = new Account(root); var working = new Account(root) { Maximum = 0 };
        using var owner = Compile("(?=(a+))a+", programs); var held = programs.Used;
        var error = Assert.Throws<Utf8MemoryAdmissionException>(() =>
        {
            if (exists) _ = owner.IsMatch("aaaa"u8, workingMemoryAllowance: working); else _ = owner.Count("aaaa"u8, workingMemoryAllowance: working);
        });
        Assert.True(error.RequestedBytes > 0); Assert.Equal(1, working.Attempts);
        Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
        working.Maximum = 1 << 20;
        Assert.Equal(1, owner.Count("aaaa"u8, workingMemoryAllowance: working)); Assert.Equal(0UL, working.Used);
    }

    [Fact]
    public void CompilationFailureAndOriginalCancellationRefundDiscardedStorage()
    {
        var root = new SharedRoot(); var account = new Account(root); Assert.True(account.TryReserve(13));
        Assert.ThrowsAny<ArgumentException>(() => Compile("(", account)); Assert.Equal(13UL, account.Used);
        using var canceled = new CancellationTokenSource(); account.AfterGrant = canceled.Cancel;
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => Compile("(a+)", account, token: canceled.Token)).CancellationToken);
        Assert.Equal(13UL, account.Used); account.AfterGrant = null; account.Maximum = account.Used;
        Assert.Throws<Utf8MemoryAdmissionException>(() => Compile("alpha", account)); Assert.Equal(13UL, account.Used);
        account.Release(13);
    }

    [Fact]
    public void UnlimitedSharedCompilationDoesNotAcquireAPrivateByteCap()
    {
        var root = new SharedRoot(); var account = new Account(root); ulong maximum = 0;
        account.AfterGrant = () => maximum = Utf8ConstructionScope.CurrentBudget.MaximumBytes;
        using (var owner = Compile("alpha", account)) Assert.Equal(ulong.MaxValue, maximum);
        Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public async Task CloseDuringActiveWorkDefersProgramRefundAndRejectsNewCalls()
    {
        var root = new SharedRoot(); var programs = new Account(root); var working = new Account(root);
        using var owner = Compile("needle", programs); var held = programs.Used;
        var input = Encoding.UTF8.GetBytes(new string('x', 262144) + "needle");
        using var entered = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        // The worker deliberately blocks inside production execution. A dedicated
        // thread avoids starving it behind synchronous test-runner pool work.
        var task = Task.Factory.StartNew(() =>
        {
            var paused = false;
            var session = Utf8SearchDiagnosticsSession.Start(_ =>
            {
                if (paused) return; paused = true; entered.Set();
                if (!resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Host did not resume the active call.");
            });
            try { return owner.Count(input, workingMemoryAllowance: working); } finally { session.Complete(); }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10))); owner.Dispose();
            Assert.Equal(held, programs.Used);
            Assert.Throws<ObjectDisposedException>(() => owner.IsMatch(input));
        }
        finally { resume.Set(); await task; }
        Assert.Equal(1, task.Result); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public async Task IndependentConcurrentCallsHaveSeparateWorkingLoans()
    {
        var root = new SharedRoot(); var programs = new Account(root); var working = new Account(root);
        using var owner = Compile("([a-z]+)-([0-9]+)", programs); var held = programs.Used;
        var input = "aa-1 bb-22 cc-333"u8.ToArray();
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => owner.Count(input, workingMemoryAllowance: working))));
        Assert.All(results, result => Assert.Equal(3, result));
        Assert.Equal(0UL, working.Used); Assert.Equal(held, root.Used);
    }

    [Fact]
    public void StrictValidationOriginalInterruptionAndNumericAdmissionStayAuthoritative()
    {
        var root = new SharedRoot(); var account = new Account(root); byte[] malformed = [0xf0, 0x9f];
        using (var owner = Compile("alpha", account))
        {
            var held = account.Used;
            Assert.Throws<ArgumentException>(() => owner.Count(malformed));
            using var token = new CancellationTokenSource(); token.Cancel();
            Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => owner.IsMatch(malformed, token.Token)).CancellationToken);
            Assert.Equal(held, account.Used);
        }
        using (var work = Compile("(a+)", account, policy: new() { MaxWorkSteps = 1 }))
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, Assert.Throws<Utf8ResourceLimitExceededException>(() => work.Count("aaaa"u8)).ResourceKind);
        using (var bytes = Compile("(a+)", account, policy: new() { MaxWorkingBytes = 1 }))
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, Assert.Throws<Utf8ResourceLimitExceededException>(() => bytes.Count("aaaa"u8)).ResourceKind);
        using (var native = Compile("alpha", account, policy: new() { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 }))
            Assert.Equal(2, native.Count("alpha alpha"u8));
        Assert.Equal(0UL, root.Used);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ActualExecutionInterruptionRefundsWorkingLoansWithoutClosingTheProgram(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var root = new SharedRoot(); var programs = new Account(root); var working = new Account(root);
        using var token = new CancellationTokenSource();
        using var owner = Utf8Regex.Compile("([a-z]+)-([0-9]+)", RegexOptions.None,
            timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan, default, default,
            Timeout.InfiniteTimeSpan, default, programs);
        var held = programs.Used;
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("alpha-123 ", 1024)));
        var validation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Timeout.InfiniteTimeSpan, default);
        _ = Utf8Validation.Validate(input, ref validation); var validatedSteps = validation.Budget.Steps;
        var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= validatedSteps) return;
            reached = true; if (timeout) ticks = 100; else token.Cancel();
        });
        try
        {
            var failure = Record.Exception(() => owner.Count(input, token.Token, working)); Assert.True(reached);
            if (timeout) { var error = Assert.IsType<RegexMatchTimeoutException>(failure); Assert.Empty(error.Input); }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(working.Attempts > 0); Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
        ticks = 0;
        Assert.Equal(1, owner.Count("alpha-1"u8, workingMemoryAllowance: working));
    }

    [Fact]
    public void LogicalDepthStillAppliesWithSharedFunding()
    {
        var root = new SharedRoot(); var account = new Account(root);
        using var owner = Compile("(?=(?=(a)))a", account, policy: new() { MaxDepth = 1 });
        var held = account.Used;
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => owner.IsMatch("a"u8)).ResourceKind);
        Assert.Equal(held, account.Used);
    }

    private sealed class SharedRoot
    {
        internal readonly object Gate = new();
        internal ulong Used;
        internal ulong Maximum = 64 << 20;
    }

    private sealed class Account(SharedRoot root) : IUtf8MemoryAllowance
    {
        internal ulong Used;
        internal ulong Maximum = ulong.MaxValue;
        internal int Attempts;
        internal Action? AfterGrant;
        public bool TryReserve(ulong bytes)
        {
            lock (root.Gate)
            {
                Attempts++;
                if (bytes > Maximum - Math.Min(Maximum, Used) || bytes > root.Maximum - root.Used) return false;
                Used += bytes; root.Used += bytes;
            }
            AfterGrant?.Invoke(); return true;
        }
        public void Release(ulong bytes)
        {
            lock (root.Gate)
            {
                if (bytes > Used) throw new InvalidOperationException("Unfunded release.");
                Used -= bytes; root.Used -= bytes;
            }
        }
    }
}

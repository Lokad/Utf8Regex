using System.Text;
using System.Buffers;
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

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MetadataCopyUsesCallerStorageAndDoesNotOutliveProgramAdmission(bool native)
    {
        const string pattern = "(?<12>😀)";
        var encoded = Encoding.UTF8.GetBytes(pattern);
        var root = new SharedRoot(); var account = new Account(root);
        using var owner = native ? Utf8Regex.Compile(encoded, RegexOptions.None, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, account) : Compile(pattern, account);
        var held = account.Used; var destination = new byte[encoded.Length];
        Assert.Equal(1, owner.CaptureGroupCount);
        Assert.Equal(12, owner.GroupNumberFromUtf8Name("12"u8));
        Assert.Equal(System.Buffers.OperationStatus.Done, owner.TryCopyPatternUtf8(destination, out var required));
        Assert.Equal(encoded.Length, required); Assert.Equal(encoded, destination); Assert.Equal(held, account.Used);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            owner.TryCopyPatternUtf8(destination, out _, cancellation.Token)).CancellationToken);
        owner.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Throws<ObjectDisposedException>(() => owner.TryCopyPatternUtf8(destination, out _));
        Assert.Throws<ObjectDisposedException>(() => owner.CaptureGroupCount);
        Assert.Throws<ObjectDisposedException>(() => owner.GroupNumberFromUtf8Name("12"u8));
        Assert.Equal(encoded, destination);
    }

    [Theory]
    [InlineData("(ab[0-9]d)", "ab1d/ab2d", "é[$1]😀", RegexOptions.None)]
    [InlineData("(ab[0-9]d)", "ab1d/ab2d", "é[$1]😀", RegexOptions.Compiled)]
    [InlineData("(.)", "é😀", "[$1]", RegexOptions.None)]
    [InlineData("(?<12>é)(?<5>😀)?", "é😀/é", "$12-$5-$+", RegexOptions.None)]
    [InlineData("(a)?", "ba😀", "$`<$1>$'", RegexOptions.None)]
    [InlineData("(a|aa)", "aa", "[$1]", RegexOptions.RightToLeft)]
    [InlineData("(a|aa)", "aa", "[$1]", RegexOptions.NonBacktracking)]
    [InlineData("(a)", "😀", "${unknown}", RegexOptions.None)]
    public void FundedTemplateDestinationPreservesContractsAndSeparateOwners(string pattern, string subject, string template, RegexOptions options)
    {
        var root = new SharedRoot(); var programs = new Account(root); var working = new Account(root);
        var input = Encoding.UTF8.GetBytes(subject); var native = Encoding.UTF8.GetBytes(template);
        var expected = new Utf8Regex(pattern, options).Replace(input, native);
        var owner = Compile(pattern, programs, options); var held = programs.Used;
        var destination = new byte[expected.Length + 4];
        foreach (var warm in new[] { false, true })
        {
            destination.AsSpan().Fill(0xcc);
            Assert.Equal(OperationStatus.Done, owner.TryReplace(input, native, destination, out var written,
                workingMemoryAllowance: working));
            Assert.Equal(expected.Length, written); Assert.Equal(expected, destination[..written]);
            Assert.All(destination[written..], value => Assert.Equal(0xcc, value));
            Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
            if (expected.Length > 0)
            {
                var shortDestination = Enumerable.Repeat((byte)0xcc, expected.Length - 1).ToArray();
                Assert.Equal(OperationStatus.DestinationTooSmall, owner.TryReplace(input, native, shortDestination, out written,
                    workingMemoryAllowance: working));
                Assert.Equal(0, written); Assert.All(shortDestination, value => Assert.Equal(0xcc, value));
                Assert.Equal(0UL, working.Used);
            }
        }
        Assert.True(working.Peak > 0); owner.Dispose(); Assert.Equal(0UL, root.Used);
        Assert.Equal(expected, destination[..expected.Length]);
        Assert.Throws<ObjectDisposedException>(() => owner.TryReplace(input, native, destination, out _));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void FundedTemplateAdmissionDenialIsTerminalAndRefundsColdAndWarmLoans(bool warm)
    {
        var root = new SharedRoot(); var programs = new Account(root); var working = new Account(root);
        using var owner = Compile("(a)", programs); var held = programs.Used;
        var destination = new byte[16];
        if (warm) owner.TryReplace("a"u8, "[$1]"u8, destination, out _, workingMemoryAllowance: working);
        working.Maximum = 0; var attempts = working.Attempts;
        Assert.Throws<Utf8MemoryAdmissionException>(() => owner.TryReplace("a"u8, "[$1]"u8, destination, out _,
            workingMemoryAllowance: working));
        Assert.Equal(attempts + 1, working.Attempts); Assert.Equal(0UL, working.Used); Assert.Equal(held, root.Used);
        working.Maximum = ulong.MaxValue;
        Assert.Equal(OperationStatus.Done, owner.TryReplace("a"u8, "[$1]"u8, destination, out var written,
            workingMemoryAllowance: working));
        Assert.Equal("[a]"u8.ToArray(), destination[..written]);
        root.Maximum = held;
        Assert.Throws<Utf8MemoryAdmissionException>(() => owner.TryReplace("a"u8, "[$1]"u8, destination, out _,
            workingMemoryAllowance: working));
        Assert.Equal(0UL, working.Used); Assert.Equal(held, root.Used);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void FundedTemplateAliasesAndMalformedNoMatchAreQualified(bool warm)
    {
        var root = new SharedRoot(); var programs = new Account(root); using var owner = Compile("(a)", programs);
        var bytes = new byte[64]; "aaé[$1]😀"u8.CopyTo(bytes);
        if (warm) owner.TryReplace("aa"u8, "é[$1]😀"u8, new byte[64], out _);
        Assert.Equal(OperationStatus.Done, owner.TryReplace(bytes.AsSpan(0, 2), bytes.AsSpan(2, "é[$1]😀"u8.Length),
            bytes, out var written));
        Assert.Equal("é[a]😀é[a]😀"u8.ToArray(), bytes[..written]);
        var held = programs.Used;
        Assert.Throws<ArgumentException>(() => owner.TryReplace("none"u8, new byte[] { 0xff }, bytes, out _));
        Assert.Throws<ArgumentException>(() => owner.TryReplace(new byte[] { 0xff }, "$1"u8, bytes, out _));
        Assert.Equal(held, root.Used);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            owner.TryReplace([], [], bytes, out _, canceled.Token)).CancellationToken);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void FundedTemplateAdmissionInterruptionKeepsOriginalAuthority(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var root = new SharedRoot(); var programs = new Account(root); var working = new Account(root);
        using var owner = Utf8Regex.Compile("(a)", RegexOptions.None,
            timeout ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan, default, default,
            Timeout.InfiniteTimeSpan, default, programs);
        var held = programs.Used; using var canceled = new CancellationTokenSource();
        working.AfterGrant = () => { if (timeout) ticks = 20; else canceled.Cancel(); };
        var error = Record.Exception(() => owner.TryReplace("a"u8, "é[$1]"u8, new byte[32], out _, canceled.Token, working));
        if (timeout) Assert.Empty(Assert.IsType<RegexMatchTimeoutException>(error).Input);
        else Assert.Equal(canceled.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
    }

    [Fact]
    public void FundedTemplateCallRetainsProgramAcrossReentrantClose()
    {
        var root = new SharedRoot(); var programs = new Account(root); var working = new Account(root);
        var owner = Compile("(a)", programs); var held = programs.Used;
        working.AfterGrant = () => { owner.Dispose(); Assert.Equal(held, programs.Used); };
        var destination = new byte[16];
        Assert.Equal(OperationStatus.Done, owner.TryReplace("aa"u8, "[$1]"u8, destination, out var written,
            workingMemoryAllowance: working));
        Assert.Equal("[a][a]"u8.ToArray(), destination[..written]); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void FundedTemplateNumericWorkingLimitIncludesPreparationOutputOverlap()
    {
        var root = new SharedRoot(); var programs = new Account(root); var working = new Account(root);
        var destination = new byte[16]; ulong coldPeak; ulong warmPeak;
        using (var probe = Compile("(a)", programs))
        {
            probe.TryReplace("aa"u8, "é[$1]"u8, destination, out _, workingMemoryAllowance: working);
            coldPeak = working.Peak; working.Peak = 0;
            probe.TryReplace("aa"u8, "é[$1]"u8, destination, out _, workingMemoryAllowance: working);
            warmPeak = working.Peak;
        }
        Assert.True(coldPeak > warmPeak); Assert.Equal(0UL, root.Used);
        using var owner = Compile("(a)", programs, policy: new() { MaxWorkingBytes = coldPeak - 1 });
        var held = programs.Used;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => owner.TryReplace(
            "aa"u8, "é[$1]"u8, destination, out _, workingMemoryAllowance: working));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
        Assert.Equal(coldPeak - 1, failure.Limit);
        Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
        // Preparation published a valid plan under prepaid program admission.
        // Reuse no longer overlaps cold scratch, so the same cap now admits it.
        Assert.Equal(OperationStatus.Done, owner.TryReplace("aa"u8, "é[$1]"u8, destination,
            out var written, workingMemoryAllowance: working));
        Assert.Equal("é[a]é[a]"u8.ToArray(), destination[..written]); Assert.Equal(0UL, working.Used);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void FundedTemplateOtherNumericControlsStillApply(int control)
    {
        var root = new SharedRoot(); var programs = new Account(root); var working = new Account(root);
        var policy = control switch { 0 => new Utf8ResourcePolicy { MaxWorkSteps = 1 },
            1 => new Utf8ResourcePolicy { MaxDepth = 1 }, _ => new Utf8ResourcePolicy { MaxResultBytes = 1 } };
        using var owner = Compile("(?=(?=(a)))a", programs, policy: policy); var held = programs.Used;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => owner.TryReplace(
            "a"u8, "[$1]"u8, new byte[16], out _, workingMemoryAllowance: working));
        Assert.Equal(control switch { 0 => Utf8ResourceLimitKind.ExecutionWorkSteps,
            1 => Utf8ResourceLimitKind.ExecutionDepth, _ => Utf8ResourceLimitKind.ExecutionResultBytes }, failure.ResourceKind);
        Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
    }

    private sealed class SharedRoot
    {
        internal readonly object Gate = new();
        internal ulong Used;
        internal ulong Peak;
        internal ulong Maximum = 64 << 20;
    }

    private sealed class Account(SharedRoot root) : IUtf8MemoryAllowance
    {
        internal ulong Used;
        internal ulong Peak;
        internal ulong Maximum = ulong.MaxValue;
        internal int Attempts;
        internal Action? AfterGrant;
        public bool TryReserve(ulong bytes)
        {
            lock (root.Gate)
            {
                Attempts++;
                if (bytes > Maximum - Math.Min(Maximum, Used) || bytes > root.Maximum - root.Used) return false;
                Used += bytes; root.Used += bytes; Peak = Math.Max(Peak, Used); root.Peak = Math.Max(root.Peak, root.Used);
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

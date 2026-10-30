using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2FundedProgramOwnerTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void SeparateAccountsKeepCompilationFundedAndRefundTemporaryLoans(bool bytes)
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        Assert.True(programs.TryReserve(13)); using var construction = new CancellationTokenSource();
        var owner = Compile("(?<=foo)(?<word>[a-z]+)-([0-9]+)", programs, bytes: bytes, token: construction.Token);
        var held = programs.Used; construction.Cancel();
        var subject = "fooabc-123 foofoo-45"u8.ToArray();
        Assert.Equal(2, owner.Count(subject, workingMemoryAllowance: working)); Assert.True(owner.IsMatch(subject, workingMemoryAllowance: working));
        Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
        Assert.Equal(2, owner.Count(subject)); Assert.Equal(held, programs.Used);
        owner.Dispose(); owner.Dispose(); Assert.Equal(13UL, root.Used);
        Assert.Throws<ObjectDisposedException>(() => owner.IsMatch(subject)); Assert.Throws<ObjectDisposedException>(() => owner.Count(subject));
        programs.Release(13); Assert.Equal(0UL, root.Used);
    }

    [Theory]
    [InlineData(".", "😀é", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData("(?<=foo)([a-z]+)", "😀fooabc foofoo", Pcre2CompileOptions.None, 4, Pcre2MatchOptions.None)]
    [InlineData("a*|b", "ba", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData("(?<x>a)(?P=x)", "aa a", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData("abc", "aBc ABC", Pcre2CompileOptions.Caseless, 0, Pcre2MatchOptions.None)]
    [InlineData(@"\w+", "éé foo", Pcre2CompileOptions.Ucp, 0, Pcre2MatchOptions.None)]
    [InlineData("a|aa", "xaa", Pcre2CompileOptions.None, 1, Pcre2MatchOptions.Anchored)]
    [InlineData("a*", "ba", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.NotEmpty)]
    public void SharedWorkingAndOwnerPreserveScalarFlagsOffsetsAndProgression(string pattern, string text,
        Pcre2CompileOptions options, int start, Pcre2MatchOptions matchOptions)
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        var input = Encoding.UTF8.GetBytes(text); var regex = new Utf8Pcre2Regex(pattern, options);
        var count = regex.Count(input, start, matchOptions); var exists = regex.IsMatch(input, start, matchOptions);
        Assert.Equal(count, regex.Count(input, start, matchOptions, default, working));
        Assert.Equal(exists, regex.IsMatch(input, start, matchOptions, default, working)); Assert.Equal(0UL, working.Used);
        using (var owner = Compile(pattern, programs, options: options))
        {
            var held = programs.Used;
            Assert.Equal(count, owner.Count(input, start, matchOptions, workingMemoryAllowance: working));
            Assert.Equal(exists, owner.IsMatch(input, start, matchOptions, workingMemoryAllowance: working));
            Assert.Equal(held, root.Used);
        }
        Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void BytePatternOwnsItsBehaviorAndConstructionFailureRefundsOnlyItsOwnAccount()
    {
        var root = new Root(); var account = new Account(root); Assert.True(account.TryReserve(13));
        var pattern = "é+"u8.ToArray();
        using (var owner = Utf8Pcre2Regex.Compile(pattern, Pcre2CompileOptions.None, default, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, account))
        { Array.Fill(pattern, (byte)'x'); Assert.Equal(1, owner.Count("éé"u8)); }
        Assert.Equal(13UL, root.Used);
        Assert.Throws<Pcre2CompileException>(() => Compile("(?<123>a)", account)); Assert.Equal(13UL, root.Used);
        Assert.Throws<ArgumentException>(() => Utf8Pcre2Regex.Compile(new byte[] { 0xff }, Pcre2CompileOptions.None,
            default, Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, account));
        Assert.Equal(13UL, root.Used);
        using var canceled = new CancellationTokenSource(); account.AfterGrant = canceled.Cancel;
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => Compile("(a+)", account, token: canceled.Token)).CancellationToken);
        Assert.Equal(13UL, root.Used); account.AfterGrant = null; account.Maximum = 13;
        Assert.Throws<Utf8MemoryAdmissionException>(() => Compile("alpha", account)); Assert.Equal(13UL, root.Used);
        account.Release(13);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void WorkingRefusalIsTerminalAndDoesNotCloseTheProgram(bool exists)
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root) { Maximum = 0 };
        using var owner = Compile("(?<x>a+)(?=b)b", programs); var held = programs.Used;
        Assert.Throws<Utf8MemoryAdmissionException>(() =>
        {
            if (exists) _ = owner.IsMatch("aaab"u8, workingMemoryAllowance: working);
            else _ = owner.Count("aaab"u8, workingMemoryAllowance: working);
        });
        Assert.Equal(1, working.Attempts); Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
        working.Maximum = 1 << 20; Assert.Equal(1, owner.Count("aaab"u8, workingMemoryAllowance: working));
        Assert.Equal(0UL, working.Used);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ActualWorkingCancellationAndDeadlineRefundBeforeThrowing(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        using var current = new CancellationTokenSource();
        using var owner = Compile("(?<x>a+)(?=b)b", programs, matchTimeout: timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan);
        var held = programs.Used;
        working.AfterGrant = () => { if (timeout) ticks = 100; else current.Cancel(); };
        var failure = Record.Exception(() => owner.Count("aaab"u8, cancellationToken: current.Token, workingMemoryAllowance: working));
        if (timeout) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        else Assert.Equal(current.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.True(working.Attempts > 0); Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
        ticks = 0; working.AfterGrant = null;
        Assert.True(owner.IsMatch("aaab"u8, workingMemoryAllowance: working)); Assert.Equal(0UL, working.Used);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void NumericDenialsRetainPcre2ErrorVocabulary(int kind)
    {
        var policy = kind switch
        {
            0 => new Utf8ResourcePolicy { MaxWorkSteps = 1 },
            1 => new Utf8ResourcePolicy { MaxWorkingBytes = 1 },
            _ => new Utf8ResourcePolicy { MaxDepth = 1 }
        };
        const string pattern = "(?=(?=(a+)))a+";
        var baseline = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.None, default, policy, Timeout.InfiniteTimeSpan);
        var expected = Assert.Throws<Pcre2MatchException>(() => baseline.Count("aaaa"u8));
        var root = new Root(); var account = new Account(root);
        using var owner = Compile(pattern, account, policy: policy); var held = account.Used;
        Assert.Equal(expected.ErrorKind, Assert.Throws<Pcre2MatchException>(() => owner.Count("aaaa"u8)).ErrorKind);
        Assert.Equal(held, account.Used);
    }

    [Fact]
    public void StrictInputBoundaryAndPreCancellationRemainAuthoritative()
    {
        var root = new Root(); var account = new Account(root);
        using var owner = Compile(".", account); var held = account.Used;
        Assert.Throws<ArgumentException>(() => owner.Count(new byte[] { (byte)'a', 0xff }));
        Assert.Throws<ArgumentOutOfRangeException>(() => owner.Count("😀x"u8, 1)); Assert.Equal(held, account.Used);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            owner.IsMatch(new byte[] { 0xff }, cancellationToken: canceled.Token)).CancellationToken);
        Assert.Equal(held, account.Used);
    }

    [Fact]
    public void UnlimitedSharedConstructionDoesNotAddAPrivateByteCap()
    {
        var root = new Root(); var account = new Account(root); ulong maximum = 0;
        account.AfterGrant = () => maximum = Utf8ConstructionScope.CurrentBudget.MaximumBytes;
        using (var owner = Compile("alpha", account)) Assert.Equal(ulong.MaxValue, maximum);
        Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public async Task ClosingDuringAnActiveCallDefersRefundAndRejectsNewCalls()
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        var owner = Compile("(?<x>a+)(?=b)b", programs); var held = programs.Used;
        using var entered = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        var paused = false;
        working.AfterGrant = () =>
        {
            if (paused) return; paused = true; entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test call was not resumed.");
        };
        var call = Task.Run(() => owner.Count("aaab"u8, workingMemoryAllowance: working));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10))); owner.Dispose(); Assert.Equal(held, programs.Used);
            Assert.Throws<ObjectDisposedException>(() => owner.Count("aaab"u8));
        }
        finally { resume.Set(); }
        Assert.Equal(1, await call); Assert.Equal(0UL, root.Used); owner.Dispose();
    }

    [Fact]
    public async Task IndependentConcurrentCallsShareTheRootWithoutSharingInvocationState()
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        using var owner = Compile("(?<x>a+)(?=b)b", programs); var held = programs.Used;
        var calls = Enumerable.Range(0, 16).Select(_ => Task.Run(() => owner.Count("aaab aab"u8, workingMemoryAllowance: working)));
        Assert.All(await Task.WhenAll(calls), value => Assert.Equal(2, value));
        Assert.Equal(0UL, working.Used); Assert.Equal(held, root.Used);
    }

    [Fact]
    public void DifferentChildrenCannotEscapeTheSharedRootCeiling()
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        using var owner = Compile("(?<x>a+)(?=b)b", programs); var held = programs.Used;
        root.Maximum = held;
        Assert.Throws<Utf8MemoryAdmissionException>(() => owner.Count("aaab"u8, workingMemoryAllowance: working));
        Assert.Equal(1, working.Attempts); Assert.Equal(held, root.Used);
        root.Maximum += 1 << 20; Assert.Equal(1, owner.Count("aaab"u8, workingMemoryAllowance: working));
        Assert.Equal(held, root.Used);
    }

    private static Utf8Pcre2RegexProgramOwner Compile(string pattern, Account account, bool bytes = false,
        Pcre2CompileOptions options = Pcre2CompileOptions.None, Utf8ResourcePolicy policy = default,
        CancellationToken token = default, TimeSpan? matchTimeout = null)
        => bytes ? Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(pattern), options, default, matchTimeout ?? Timeout.InfiniteTimeSpan,
            policy, default, Timeout.InfiniteTimeSpan, token, account)
        : Utf8Pcre2Regex.Compile(pattern, options, default, matchTimeout ?? Timeout.InfiniteTimeSpan,
            policy, default, Timeout.InfiniteTimeSpan, token, account);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MetadataCopyUsesCallerStorageAndRetainsValidatedCaptureCount(bool native)
    {
        const string pattern = "(?|(a)|(b))(?<x>😀)";
        var encoded = Encoding.UTF8.GetBytes(pattern);
        var root = new Root(); var account = new Account(root);
        using var owner = Compile(pattern, account, bytes: native);
        var held = account.Used; var destination = new byte[encoded.Length];
        Assert.Equal(2, owner.CaptureGroupCount);
        var numbers = new int[1];
        Assert.Equal(1, owner.CopyNumbersForUtf8Name("x"u8, numbers, out var more));
        Assert.False(more); Assert.Equal(2, numbers[0]);
        Assert.Equal(System.Buffers.OperationStatus.Done, owner.TryCopyPatternUtf8(destination, out var required));
        Assert.Equal(encoded.Length, required); Assert.Equal(encoded, destination); Assert.Equal(held, account.Used);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            owner.TryCopyPatternUtf8(destination, out _, cancellation.Token)).CancellationToken);
        owner.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Throws<ObjectDisposedException>(() => owner.TryCopyPatternUtf8(destination, out _));
        Assert.Throws<ObjectDisposedException>(() => owner.CaptureGroupCount);
        Assert.Throws<ObjectDisposedException>(() => owner.CopyNumbersForUtf8Name("x"u8, numbers, out _));
        Assert.Equal(encoded, destination);
    }

    private sealed class Root { internal readonly object Gate = new(); internal ulong Used; internal ulong Maximum = 64 << 20; }
    private sealed class Account(Root root) : IUtf8MemoryAllowance
    {
        internal ulong Used; internal ulong Maximum = 64 << 20; internal int Attempts; internal Action? AfterGrant;
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

using System.Text;
using System.Buffers;
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

    [Theory]
    [InlineData("a", "aa/a", "[$0]", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None)]
    [InlineData("(a+)", "aa/a", "é[$1]😀", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None)]
    [InlineData("(?<x>a+)(😀)", "aa😀/a😀", @"\U${x}\E", 0, Pcre2SubstitutionOptions.Extended, Pcre2MatchOptions.None)]
    [InlineData("(?<=foo)([a-z]+)", "😀fooabc/fooz", "[$1]", 4, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None)]
    [InlineData("(.)", "😀é", "[$0]", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None)]
    [InlineData("(?|(a)|(b))", "ab", "[$1]", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None)]
    [InlineData(@"foo\Kbar", "foobar/foobar", "[$0]", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None)]
    [InlineData("(a)?", "ba", "<$0:$1>", 0, Pcre2SubstitutionOptions.UnsetEmpty, Pcre2MatchOptions.None)]
    [InlineData("a", "aa/a", "$0", 0, Pcre2SubstitutionOptions.SubstituteLiteral, Pcre2MatchOptions.None)]
    [InlineData("(a+)", "aa/a", "[$1]", 0, Pcre2SubstitutionOptions.SubstituteReplacementOnly, Pcre2MatchOptions.None)]
    [InlineData("(a)", "none", "${missing}", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None)]
    [InlineData("a*", "ba", "[$0]", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.NotEmpty)]
    [InlineData("(*MARK:pear)x", "xx", "$*MARK", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None)]
    [InlineData("(x)", "x", @"${1:-\x{z}}", 0, Pcre2SubstitutionOptions.Extended, Pcre2MatchOptions.None)]
    public void FundedTemplateDestinationPreservesProfileAndSeparateOwners(string pattern, string text,
        string template, int start, Pcre2SubstitutionOptions options, Pcre2MatchOptions matchOptions)
    {
        var input = Encoding.UTF8.GetBytes(text); var source = Encoding.UTF8.GetBytes(template);
        var expected = new Utf8Pcre2Regex(pattern).Replace(input, source, start, options, matchOptions);
        foreach (var native in new[] { false, true })
        {
            var root = new Root(); var programs = new Account(root); var working = new Account(root);
            var owner = Compile(pattern, programs, bytes: native); var held = programs.Used;
            Assert.True(held >= 4UL << 20); // Existing cache payload maximum is prepaid.
            var destination = new byte[expected.Length + 4];
            for (var pass = 0; pass < 2; pass++)
            {
                destination.AsSpan().Fill(0xcc);
                Assert.Equal(OperationStatus.Done, owner.TryReplace(input, source, destination, out var written,
                    start, options, matchOptions, workingMemoryAllowance: working));
                Assert.Equal(expected.Length, written); Assert.Equal(expected, destination[..written]);
                Assert.All(destination[written..], value => Assert.Equal(0xcc, value));
                Assert.Equal(held, programs.Used); Assert.Equal(0UL, working.Used);
                if (expected.Length > 0)
                {
                    foreach (var overflow in new[] { false, true })
                    {
                        var shortDestination = Enumerable.Repeat((byte)0xcc, expected.Length - 1).ToArray();
                        Assert.Equal(OperationStatus.DestinationTooSmall, owner.TryReplace(input, source, shortDestination,
                            out written, start, options | (overflow ? Pcre2SubstitutionOptions.SubstituteOverflowLength : 0),
                            matchOptions, workingMemoryAllowance: working));
                        Assert.Equal(overflow ? expected.Length : 0, written);
                        Assert.All(shortDestination, value => Assert.Equal(0xcc, value)); Assert.Equal(0UL, working.Used);
                    }
                }
            }
            Assert.True(working.Attempts > 0); owner.Dispose(); Assert.Equal(0UL, root.Used);
            Assert.Equal(expected, destination[..expected.Length]);
            Assert.Throws<ObjectDisposedException>(() => owner.TryReplace(input, source, destination, out _));
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void FundedTemplateTerminalDenialAndProgramCacheCoverage(bool warm)
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        using var owner = Compile("(?<x>a+)", programs); var held = programs.Used; var destination = new byte[32];
        if (warm) owner.TryReplace("aa"u8, "é${x}"u8, destination, out _, workingMemoryAllowance: working);
        working.Maximum = 0; var attempts = working.Attempts;
        Assert.Throws<Utf8MemoryAdmissionException>(() => owner.TryReplace("aa"u8, "é${x}"u8, destination,
            out _, workingMemoryAllowance: working));
        Assert.Equal(attempts + 1, working.Attempts); Assert.Equal(0UL, working.Used); Assert.Equal(held, root.Used);
        working.Maximum = ulong.MaxValue;
        Assert.Equal(OperationStatus.Done, owner.TryReplace("aa"u8, "é${x}"u8, destination,
            out var written, workingMemoryAllowance: working));
        Assert.Equal("éaa"u8.ToArray(), destination[..written]); Assert.Equal(held, root.Used);
        root.Maximum = held;
        Assert.Throws<Utf8MemoryAdmissionException>(() => owner.TryReplace("aa"u8, "é${x}"u8, destination,
            out _, workingMemoryAllowance: working));
        Assert.Equal(0UL, working.Used); Assert.Equal(held, root.Used);
    }

    [Fact]
    public void FundedTemplateCacheEvictionKeepsOnlyPrepaidProgramCharge()
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        using var owner = Compile("(a)", programs); var held = programs.Used; var destination = new byte[32];
        for (var index = 0; index < 64; index++)
        {
            var prefix = index.ToString("D2"); var template = Encoding.UTF8.GetBytes(prefix + "$1");
            Assert.Equal(OperationStatus.Done, owner.TryReplace("a"u8, template, destination, out var written,
                workingMemoryAllowance: working));
            Assert.Equal(Encoding.UTF8.GetBytes(prefix + "a"), destination[..written]);
            Assert.Equal(held, root.Used); Assert.Equal(0UL, working.Used);
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void FundedTemplateAdmissionInterruptionKeepsOriginalAuthority(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        using var owner = Compile("(?<x>a+)", programs, matchTimeout: timeout ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan);
        var held = programs.Used; using var canceled = new CancellationTokenSource();
        working.AfterGrant = () => { if (timeout) ticks = 20; else canceled.Cancel(); };
        var error = Record.Exception(() => owner.TryReplace("aa"u8, "é${x}"u8, new byte[32], out _,
            cancellationToken: canceled.Token, workingMemoryAllowance: working));
        if (timeout) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(error).ErrorKind);
        else Assert.Equal(canceled.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        Assert.Equal(0UL, working.Used); Assert.Equal(held, root.Used);
    }

    [Fact]
    public void FundedTemplateRetainsProgramAcrossReentrantClose()
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        var owner = Compile("(a)", programs); var held = programs.Used;
        working.AfterGrant = () => { owner.Dispose(); Assert.Equal(held, programs.Used); };
        var destination = new byte[32];
        Assert.Equal(OperationStatus.Done, owner.TryReplace("aa"u8, "[$1]"u8, destination,
            out var written, workingMemoryAllowance: working));
        Assert.Equal("[a][a]"u8.ToArray(), destination[..written]); Assert.Equal(0UL, root.Used);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void FundedTemplateAliasesAndMalformedNoMatchPreserveValidation(bool warm)
    {
        var root = new Root(); var programs = new Account(root); using var owner = Compile("(a)", programs);
        var bytes = new byte[64]; "aaé[$1]😀"u8.CopyTo(bytes);
        if (warm) owner.TryReplace("aa"u8, "é[$1]😀"u8, new byte[64], out _);
        Assert.Equal(OperationStatus.Done, owner.TryReplace(bytes.AsSpan(0, 2), bytes.AsSpan(2, "é[$1]😀"u8.Length),
            bytes, out var written));
        Assert.Equal("é[a]😀é[a]😀"u8.ToArray(), bytes[..written]); var held = root.Used;
        Assert.Throws<ArgumentException>(() => owner.TryReplace("none"u8, new byte[] { 0xff }, bytes, out _));
        Assert.Throws<ArgumentException>(() => owner.TryReplace(new byte[] { 0xff }, "$1"u8, bytes, out _));
        Assert.Equal(held, root.Used);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => owner.TryReplace(
            [], [], bytes, out _, cancellationToken: canceled.Token)).CancellationToken);
        Assert.Equal(Pcre2ErrorKind.UnknownReplacementGroup, Assert.Throws<Pcre2SubstitutionException>(() =>
            owner.TryReplace("a"u8, "${missing}"u8, bytes, out _)).ErrorKind);
        Assert.Equal(held, root.Used);
    }

    [Fact]
    public void FundedTemplateNumericHeapLimitIncludesColdPreparationAndLiveOutput()
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        var input = Encoding.UTF8.GetBytes(new string('a', 256)); var destination = new byte[4096];
        ulong coldPeak; ulong warmPeak;
        using (var probe = Compile("(?<x>a)", programs))
        {
            probe.TryReplace(input, "é[$x]"u8, destination, out _, workingMemoryAllowance: working);
            coldPeak = working.Peak; working.Peak = 0;
            probe.TryReplace(input, "é[$x]"u8, destination, out _, workingMemoryAllowance: working);
            warmPeak = working.Peak;
        }
        Assert.True(coldPeak > warmPeak); Assert.Equal(0UL, root.Used);
        using var owner = Compile("(?<x>a)", programs, policy: new() { MaxWorkingBytes = coldPeak - 1 });
        var held = programs.Used;
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() => owner.TryReplace(
            input, "é[$x]"u8, destination, out _, workingMemoryAllowance: working)).ErrorKind);
        Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
        Assert.Equal(OperationStatus.Done, owner.TryReplace(input, "é[$x]"u8, destination, out var written,
            workingMemoryAllowance: working));
        Assert.Equal(string.Concat(Enumerable.Repeat("é[a]", 256)), Encoding.UTF8.GetString(destination, 0, written));
        Assert.Equal(0UL, working.Used);
    }

    [Fact]
    public void ProgramCacheAllowanceIsAdmittedAndInterruptedBeforeOwnerPublication()
    {
        var root = new Root(); var programs = new Account(root); Assert.True(programs.TryReserve(13));
        var refusal = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8Pcre2Regex.Compile(
            "alpha", default, default, Timeout.InfiniteTimeSpan, default, new() { MaxBytes = 1 << 20 },
            Timeout.InfiniteTimeSpan, default, programs));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, refusal.ResourceKind); Assert.Equal(13UL, root.Used);
        using var canceled = new CancellationTokenSource();
        programs.AfterGrant = () => { if (programs.Used > 4UL << 20) canceled.Cancel(); };
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            Compile("alpha", programs, token: canceled.Token)).CancellationToken);
        Assert.Equal(13UL, root.Used); programs.AfterGrant = null;
        programs.Release(13); Assert.Equal(0UL, root.Used);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void FundedTemplateOtherNumericControlsRemainEnforceable(int control)
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root);
        var policy = control switch { 0 => new Utf8ResourcePolicy { MaxWorkSteps = 1 },
            1 => new Utf8ResourcePolicy { MaxDepth = 1 }, _ => new Utf8ResourcePolicy { MaxResultBytes = 1 } };
        using var owner = Compile("(?=(?=(a)))a", programs, policy: policy); var held = programs.Used;
        Assert.Equal(control switch { 0 => Pcre2ErrorKind.MatchLimit, 1 => Pcre2ErrorKind.DepthLimit,
            _ => Pcre2ErrorKind.ResultLimit }, Assert.Throws<Pcre2MatchException>(() => owner.TryReplace(
            "a"u8, "[$1]"u8, new byte[16], out _, workingMemoryAllowance: working)).ErrorKind);
        Assert.Equal(0UL, working.Used); Assert.Equal(held, programs.Used);
    }

    private sealed class Root { internal readonly object Gate = new(); internal ulong Used; internal ulong Maximum = 64 << 20; }
    private sealed class Account(Root root) : IUtf8MemoryAllowance
    {
        internal ulong Used; internal ulong Peak; internal ulong Maximum = 64 << 20; internal int Attempts; internal Action? AfterGrant;
        public bool TryReserve(ulong bytes)
        {
            lock (root.Gate)
            {
                Attempts++;
                if (bytes > Maximum - Math.Min(Maximum, Used) || bytes > root.Maximum - root.Used) return false;
                Used += bytes; root.Used += bytes; Peak = Math.Max(Peak, Used);
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

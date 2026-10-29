using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;
using ObservedAccount = Lokad.Utf8Regex.PythonRe.Tests.PythonReRetainedRangeTests.ObservedAccount;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReRetainedSubnTests
{
    private static Utf8PythonRegex Compile(string pattern, IUtf8MemoryAllowance account,
        Utf8ResourcePolicy policy = default, TimeSpan? timeout = null) => Utf8PythonRegex.Compile(pattern,
            default, timeout ?? Timeout.InfiniteTimeSpan, policy, default, Timeout.InfiniteTimeSpan, default, account);

    [Theory]
    [InlineData("cat", "cat😀cat", "X", 1, 0, "X😀cat", 1)]
    [InlineData("(?P<x>a+)", "aa bb a", @"[\g<x>]", 0, 0, "[aa] bb [a]", 2)]
    [InlineData(".", "é😀", "z", 0, 0, "zz", 2)]
    [InlineData(".", "é😀", "z", 0, 2, "éz", 1)]
    [InlineData("a", "aaa", "x", -1, 0, "aaa", 0)]
    [InlineData("", "a", "-", 0, 0, "-a-", 2)]
    [InlineData("|a", "a", "-", 0, 0, "---", 3)]
    [InlineData("a", "aaa", "", 0, 0, "", 3)]
    [InlineData("z", "", "x", 0, 0, "", 0)]
    public void NativeScalarUnicodeAndEmptyProgressionKeepTheirSemantics(string pattern, string input,
        string replacement, int count, int start, string expected, int replacements)
    {
        var account = new Allowance(16 << 20); using var regex = Compile(pattern, account);
        using var receipt = regex.SubnRetained(Encoding.UTF8.GetBytes(input), replacement, count, start);
        Assert.Equal(expected, Encoding.UTF8.GetString(receipt.Result.ResultBytes));
        Assert.Equal(replacements, receipt.Result.ReplacementCount); Assert.True(receipt.ChargedBytes > 0);
        regex.Dispose(); Assert.Equal(receipt.ChargedBytes, account.Used);
        receipt.Dispose(); Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public async Task ResultsAndAliasesSurviveProgramDisposalAndHostWaits()
    {
        var account = new Allowance(16 << 20); Assert.True(account.TryReserve(13));
        using var regex = Compile("a", account);
        var receipt = regex.SubnRetained("aéa"u8, "😀"); var alias = receipt; var result = receipt.Result;
        regex.Dispose(); Assert.Equal(13UL + receipt.ChargedBytes, account.Used);
        await Task.Yield(); Assert.Equal("😀é😀", Encoding.UTF8.GetString(receipt.Result.ResultBytes));
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(alias.Dispose)));
        Assert.Equal(13UL, account.Used); Assert.Throws<ObjectDisposedException>(() => receipt.Result);
        Assert.Equal("😀é😀", Encoding.UTF8.GetString(result.ResultBytes)); Assert.Equal(2, result.ReplacementCount);
        account.Release(13);
    }

    [Fact]
    public void NativePublicationDoesNotReleaseAndReacquireOutputFunding()
    {
        var inner = new Allowance(16 << 20); var account = new ObservedAccount(inner); using var regex = Compile("a", account);
        regex.Subn("aaa"u8, "b"); var held = inner.Used; var releases = account.ReleaseCalls;
        using var result = regex.SubnRetained("aaa"u8, "b");
        Assert.Equal(releases, account.ReleaseCalls); Assert.Equal(held + result.ChargedBytes, inner.Used);
        result.Dispose(); Assert.Equal(held, inner.Used); Assert.Equal(releases + 1, account.ReleaseCalls);
    }

    [Theory] [InlineData("a", "xx")] [InlineData("(?P<x>a)", @"\g<x>x")]
    public void PartialAdmissionAndOutputFailureRestoreOnlyTemporaryLoans(string pattern, string template)
    {
        var account = new Allowance(16 << 20); using var regex = Compile(pattern, account);
        regex.Subn("a"u8, template); var held = account.Used;
        var input = Enumerable.Repeat((byte)'a', 4096).ToArray(); var denied = 0; var succeeded = 0;
        for (ulong remaining = 128; remaining <= 65536; remaining *= 2)
        {
            account.Maximum = held + remaining;
            try { using var result = regex.SubnRetained(input, template); Assert.Equal(8192, result.Result.ResultBytes.Length); succeeded++; }
            catch (Utf8MemoryAdmissionException) { denied++; }
            Assert.Equal(held, account.Used);
        }
        Assert.True(denied > 0); Assert.True(succeeded > 0);
    }

    [Fact]
    public void InvalidTemplatesValidateOnNoMatchAndKeepValidCachedPlans()
    {
        var account = new Allowance(16 << 20); using var regex = Compile("(a)", account);
        regex.Subn("a"u8, @"\g<1>"); var held = account.Used;
        Assert.Throws<IndexOutOfRangeException>(() => regex.SubnRetained(""u8, @"\g<missing>")); Assert.Equal(held, account.Used);
        using var result = regex.SubnRetained("a"u8, @"\g<1>"); Assert.Equal("a", Encoding.UTF8.GetString(result.Result.ResultBytes));
        result.Dispose(); Assert.Equal(held, account.Used);
    }

    [Fact]
    public void RetainedOutputsCanRefuseNewResultsUntilExplicitRelease()
    {
        var account = new Allowance(16 << 20); using var regex = Compile("z", account);
        regex.Subn("a"u8, "x"); var held = account.Used; account.Maximum = held + 32768;
        var retained = new List<Utf8PythonRetainedSubnResult>(); var input = new byte[4096]; input.AsSpan().Fill((byte)'a');
        try
        {
            Assert.Throws<Utf8MemoryAdmissionException>(() => { for (var i = 0; i < 100; i++) retained.Add(regex.SubnRetained(input, "x")); });
            Assert.NotEmpty(retained);
            Assert.Equal(held + (ulong)retained.Sum(r => checked((long)r.ChargedBytes)), account.Used);
        }
        finally { foreach (var result in retained) result.Dispose(); }
        Assert.Equal(held, account.Used);
        for (var i = 0; i < 100; i++) { using var result = regex.SubnRetained(input, "x"); }
        Assert.Equal(held, account.Used);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void OriginalCancellationAndDeadlineRefundUnpublishedOutput(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var account = new Allowance(16 << 20); using var regex = Compile("(a+)", account,
            timeout: timeout ? TimeSpan.FromMilliseconds(100) : null);
        regex.Subn("a"u8, "x"); var held = account.Used; using var token = new CancellationTokenSource();
        var grants = 0; account.AfterGrant = () => grants++;
        using (regex.SubnRetained("aaa"u8, "x", cancellationToken: token.Token)) { }
        Assert.True(grants > 0); Assert.Equal(held, account.Used);
        var currentGrant = 0;
        account.AfterGrant = () => { if (++currentGrant == grants) { if (timeout) ticks = 100; else token.Cancel(); } };
        var error = Record.Exception(() => regex.SubnRetained("aaa"u8, "x", cancellationToken: token.Token));
        account.AfterGrant = null;
        if (timeout) { var failure = Assert.IsType<RegexMatchTimeoutException>(error); Assert.Empty(failure.Input); Assert.Equal(regex.Pattern, failure.Pattern); }
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        Assert.Equal(grants, currentGrant); Assert.Equal(held, account.Used);
    }

    [Fact]
    public void ExplicitResultTrafficLimitIncludesReceiptAcquisition()
    {
        var account = new Allowance(16 << 20); using var regex = Compile("a", account, new Utf8ResourcePolicy { MaxResultBytes = 1 });
        regex.Subn(""u8, ""); var held = account.Used;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.SubnRetained(""u8, ""));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, failure.ResourceKind); Assert.Equal(held, account.Used);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void NumericWorkDepthAndWorkingLimitsRemainAuthoritative(int kind)
    {
        var policy = kind switch
        {
            0 => new Utf8ResourcePolicy { MaxWorkSteps = 1 },
            1 => new Utf8ResourcePolicy { MaxDepth = 1 },
            _ => new Utf8ResourcePolicy { MaxWorkingBytes = 4096 },
        };
        var account = new Allowance(16 << 20); using var regex = Compile("(a+)", account, policy);
        var held = account.Used;
        var input = Encoding.UTF8.GetBytes(new string('a', kind == 2 ? 32768 : 4));
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.SubnRetained(input, @"\g<1>"));
        Assert.Equal(kind switch { 0 => Utf8ResourceLimitKind.ExecutionWorkSteps,
            1 => Utf8ResourceLimitKind.ExecutionDepth, _ => Utf8ResourceLimitKind.ExecutionWorkingBytes }, failure.ResourceKind);
        // A valid replacement plan may stay cached; closing the expression refunds it too.
        regex.Dispose(); Assert.Equal(0UL, account.Used); Assert.True(held > 0);
    }

    [Fact]
    public void StrictValidationAndScalarOffsetsApplyBeforeOutputPublication()
    {
        var account = new Allowance(16 << 20); using var regex = Compile("a", account);
        regex.Subn("a"u8, "x"); var held = account.Used;
        Assert.Throws<ArgumentException>(() => regex.SubnRetained(new byte[] { (byte)'a', 0xf0, 0x9f }, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.SubnRetained("😀a"u8, "x", startOffsetInBytes: 1));
        Assert.Equal(held, account.Used);
    }

    [Fact]
    public void DisposedReceiptsDropTheirOutputPayloadAndKeepOnlyChargeRecord()
    {
        var (receipt, output) = DisposedOutput();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Assert.False(output.IsAlive); GC.KeepAlive(receipt);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Utf8PythonRetainedSubnResult, WeakReference) DisposedOutput()
    {
        var account = new Allowance(16 << 20); using var regex = Compile("z", account);
        var receipt = regex.SubnRetained(Enumerable.Repeat((byte)'a', 1048576).ToArray(), "x");
        var output = new WeakReference(receipt.Result.ResultBytes); receipt.Dispose(); return (receipt, output);
    }

    [Fact]
    public void UnfundedUseIsExplicitAndPreCancellationKeepsItsIdentity()
    {
        var regex = new Utf8PythonRegex("a"); using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.SubnRetained("a"u8, "x", cancellationToken: token.Token)).CancellationToken);
        Assert.Throws<InvalidOperationException>(() => regex.SubnRetained("a"u8, "x"));
        Assert.Equal("x", Encoding.UTF8.GetString(regex.Subn("a"u8, "x").ResultBytes));
    }
}

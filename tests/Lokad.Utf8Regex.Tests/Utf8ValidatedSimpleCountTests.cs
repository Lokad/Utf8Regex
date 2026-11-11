using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8ValidatedSimpleCountTests
{
    private static Utf8ResourcePolicy Limits => new()
        { MaxWorkSteps = 1_000_000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 };

    [Theory]
    [InlineData("((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))", "", 0)]
    [InlineData("((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))", "é😀ASIAABCDEFGHIJKLMNOP / AKIAAAAA😀AAAAAAAAAAAA / AIDAABCDEFGHIJKLMNOP", 2)]
    [InlineData("([A-Z]{2})([0-9]{4})", "é😀AB1234 A😀1234 CD5678", 2)]
    [InlineData("(?:cat|horse)[0-9]{2,4}", "é😀cat12345 horse12 horse😀12", 2)]
    [InlineData("([a-z]+)-([0-9]+)", "é😀item-12 other-345 a😀-12", 2)]
    [InlineData("^([a-z]{2})$", "é😀ab", 0)]
    [InlineData("(?i:K)[0-9]{2}", "K12 k34 K56", 3)]
    [InlineData("[^a-z]{2}", "😀é", 1)]
    [InlineData(".{2}", "😀éZ", 1)]
    [InlineData(@"\bcat\b", "écat cat😀", 1)]
    [InlineData(@"(a)(?:x|b)\1", "axa aba ax😀a", 2)]
    public void CountKeepsScalarResultsAndOneSubjectAuthority(string pattern, string text, int expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        foreach (var policy in Enum.GetValues<Utf8InputValidation>())
        foreach (var mode in new[] { 0, 1, 2 })
        {
            var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant,
                resourcePolicy: mode == 0 ? default : Limits, inputValidation: policy);
            var account = new Account();
            account.TryReserve(17);
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                var result = mode == 0 ? regex.Count(input) : mode == 1 ? regex.Count(input, token.Token)
                    : regex.Count(input, token.Token, account);
                Assert.Equal(expected, result);
                Assert.InRange(session.SubjectValidations, 0, policy == Utf8InputValidation.Validate ? 1 : 0);
                Assert.Equal(17UL, account.Used);
            }
            finally { session.Complete(); account.Release(17); }
            Assert.Equal(0UL, account.Used);
            if (policy == Utf8InputValidation.Validate)
                Assert.Throws<ArgumentException>(() => regex.Count([.. input, 0xff], token.Token));
        }
    }

    [Theory]
    [InlineData(RegexOptions.RightToLeft)] [InlineData(RegexOptions.NonBacktracking)]
    public void OtherExecutionContractsKeepTheirScalarCounter(RegexOptions options)
    {
        var regex = new Utf8Regex("([A-Z]{2})([0-9]{4})", options, resourcePolicy: Limits,
            inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(2, regex.Count("é😀AB1234 A😀1234 CD5678"u8));
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate, RegexOptions.None, false)]
    [InlineData(Utf8InputValidation.Validate, RegexOptions.Compiled, true)]
    [InlineData(Utf8InputValidation.AssumeValid, RegexOptions.None, true)]
    [InlineData(Utf8InputValidation.AssumeValid, RegexOptions.Compiled, false)]
    public void CompleteClassRunsFitBoundedWorkWithoutExpandedBranchRetries(
        Utf8InputValidation policy, RegexOptions options, bool unicodePrefix)
    {
        var input = Encoding.UTF8.GetBytes((unicodePrefix ? "é😀" : "") +
            string.Concat(Enumerable.Repeat("aaaaaaa ", 256)) + "abcdefghijklm");
        var regex = new Utf8Regex("[A-Za-z]{8,13}", options, resourcePolicy: Limits with
            { MaxWorkSteps = 4096, MaxWorkingBytes = 1, MaxDepth = 1 }, inputValidation: policy);
        using var token = new CancellationTokenSource();
        var account = new Account();
        account.TryReserve(17);
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            Assert.Equal(1, regex.Count(input));
            Assert.Equal(1, regex.Count(input, token.Token));
            Assert.Equal(1, regex.Count(input, token.Token, account));
            Assert.Equal(policy == Utf8InputValidation.Validate ? 3 : 0, session.SubjectValidations);
            Assert.Equal(17UL, account.Used);
        }
        finally { session.Complete(); account.Release(17); }
        Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void CompleteAsciiBranchesDoNotNeedVmScratchAndStillRefuseWork()
    {
        const string pattern = "((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))";
        var regex = new Utf8Regex(pattern, resourcePolicy: Limits with { MaxWorkingBytes = 1, MaxDepth = 1 },
            inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(1, regex.Count("é😀ASIAABCDEFGHIJKLMNOP"u8));
        var denied = new Utf8Regex(pattern, resourcePolicy: Limits with { MaxWorkSteps = 1 },
            inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count("é😀ASIAABCDEFGHIJKLMNOP"u8)).ResourceKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MixedSimpleCountRetainsOriginalInterruption(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        const string pattern = "((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))";
        var regex = new Utf8Regex(pattern, matchTimeout: TimeSpan.FromMilliseconds(20), resourcePolicy: Limits,
            inputValidation: Utf8InputValidation.AssumeValid);
        var input = Encoding.UTF8.GetBytes("é😀" + new string('x', 262144) + "ASIAABCDEFGHIJKLMNOP");
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 64) return;
            entered = true;
            if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(pattern, Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, token.Token)).Pattern);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered);
        ticks = 0;
        Assert.Equal(1, regex.Count("ASIAABCDEFGHIJKLMNOP"u8));
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used;
        public bool TryReserve(ulong bytes) { if (bytes > (1UL << 20) - Used) return false; Used += bytes; return true; }
        public void Release(ulong bytes) { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}

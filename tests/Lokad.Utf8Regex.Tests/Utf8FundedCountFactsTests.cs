using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FundedCountFactsTests
{
    private static Utf8ResourcePolicy Limits => new()
        { MaxWorkSteps = 1_000_000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 };

    [Theory]
    [InlineData(@"\p{L}", "Aé𝒜1😀", 3)]
    [InlineData(@"[\p{S}]", "A+😀𝒜", 2)]
    [InlineData(@"(?:\b)(?:[\w]){10,}(?:\b)", "abcdefghijk 1234567890 abc", 2)]
    [InlineData(@"(?:\b)(?:[\w]){10,}(?:\b)", "𐐀𐐀𐐀𐐀𐐀𐐀𐐀𐐀𐐀𐐀 abc", 1)]
    [InlineData("needle", "é😀needle needle", 2)]
    [InlineData("([A-Z]{2})([0-9]{4})", "é😀AB1234 CD5678 A😀1234", 2)]
    [InlineData(@"(a+)\1", "é😀aaaa aa", 2)]
    public void EachFundedCounterKeepsRequiredFactsAndOneValidation(string pattern, string text, int expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        foreach (var policy in Enum.GetValues<Utf8InputValidation>())
        foreach (var options in new[] { RegexOptions.None, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            if ((options & RegexOptions.NonBacktracking) != 0 && pattern == @"(a+)\1") continue;
            var regex = new Utf8Regex(pattern, options, resourcePolicy: Limits, inputValidation: policy);
            var account = new Account();
            Assert.True(account.TryReserve(17));
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                // RTL reaches this reference before its capture; that existing
                // direction-dependent program has no successful match.
                var expectedForOptions = pattern == @"(a+)\1" && options == RegexOptions.RightToLeft ? 0 : expected;
                Assert.Equal(expectedForOptions, regex.Count(input, token.Token, account));
                Assert.InRange(session.SubjectValidations, 0, policy == Utf8InputValidation.Validate ? 1 : 0);
                Assert.Equal(17UL, account.Used);
            }
            finally { session.Complete(); account.Release(17); }
            Assert.Equal(0UL, account.Used);
            if (policy == Utf8InputValidation.Validate)
            {
                Assert.Throws<ArgumentException>(() => regex.Count([.. input, 0xff], token.Token, account));
                Assert.Equal(0UL, account.Used);
            }
        }
    }

    [Fact]
    public void ByteCountersStillRefuseWorkAndObserveTheOriginalToken()
    {
        using var token = new CancellationTokenSource();
        var account = new Account();
        var regex = new Utf8Regex("needle", resourcePolicy: Limits with { MaxWorkSteps = 1 },
            inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count("é😀needle"u8, token.Token, account)).ResourceKind);
        Assert.Equal(0UL, account.Used);
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count("needle"u8, token.Token, account)).CancellationToken);
        Assert.Equal(0UL, account.Used);
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used;
        public bool TryReserve(ulong bytes) { if (bytes > (1UL << 20) - Used) return false; Used += bytes; return true; }
        public void Release(ulong bytes) { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}

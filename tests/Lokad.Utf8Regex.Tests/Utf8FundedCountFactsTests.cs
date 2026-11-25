using System.Text;
using System.Text.RegularExpressions;

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
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "😀 class A0😀record R1𐐀class Wrong struct B2", 3)]
    [InlineData(@"(a+)\1", "é😀aaaa aa", 2)]
    public void EachFundedCounterKeepsRequiredFactsAndOneValidation(string pattern, string text, int expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            if ((options & RegexOptions.NonBacktracking) != 0 && pattern == @"(a+)\1") continue;
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = Limits
            });
            var account = new Account();
            Assert.True(account.TryReserve(17));
            try
            {
                // RTL reaches this reference before its capture; that existing
                // direction-dependent program has no successful match.
                var expectedForOptions = pattern == @"(a+)\1" && options == RegexOptions.RightToLeft ? 0 : expected;
                Assert.Equal(expectedForOptions, regex.Count(input, token.Token, account));
                Assert.Equal(17UL, account.Used);
            }
            finally { account.Release(17); }
            Assert.Equal(0UL, account.Used);
        }
    }

    [Fact]
    public void ByteCountersStillRefuseWorkAndObserveTheOriginalToken()
    {
        using var token = new CancellationTokenSource();
        var account = new Account();
        var regex = new Utf8Regex("needle", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = Limits with
            {
                MaxWorkSteps = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count("é😀needle"u8, token.Token, account)).ResourceKind);
        Assert.Equal(0UL, account.Used);
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count("needle"u8, token.Token, account)).CancellationToken);
        Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void CyclicOrderedRangeAndCountUseOnlyTheirRequiredRows()
    {
        const string pattern = @"(?:ab|a)+[yz]";
        const ulong rowBytes = 2 * 9 * sizeof(int);
        var input = Encoding.UTF8.GetBytes(new string('a', 8192) + "x!aay");
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled, new() { ResourcePolicy = Limits });
        var funded = new Account(rowBytes + 7);
        Assert.True(funded.TryReserve(7));
        var match = regex.Match(input, workingMemoryAllowance: funded);
        Assert.Equal((8194, 8197), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal(7UL, funded.Used);
        Assert.Equal(1, regex.Count(input, workingMemoryAllowance: funded));
        Assert.Equal(7UL, funded.Used);
        funded.Release(7);

        var tight = new Account(rowBytes - 1);
        Assert.Equal(rowBytes, Assert.Throws<Utf8MemoryAdmissionException>(
            () => regex.Match(input, workingMemoryAllowance: tight)).RequestedBytes);
        Assert.Equal(rowBytes, Assert.Throws<Utf8MemoryAdmissionException>(
            () => regex.Count(input, workingMemoryAllowance: tight)).RequestedBytes);
        Assert.Equal(0UL, tight.Used);

        using var scalar = new Utf8Regex(pattern, RegexOptions.Compiled,
            new() { ResourcePolicy = Limits with { MaxWorkingBytes = 1 } });
        var early = Encoding.UTF8.GetBytes("aay!" + new string('!', 300));
        var small = new Account(1);
        Assert.Equal(3, scalar.Match(early, workingMemoryAllowance: small).EndOffsetInBytes);
        Assert.Equal(1, scalar.Count(early, workingMemoryAllowance: small));
        Assert.Equal(0UL, small.Used);
    }

    private sealed class Account(ulong capacity = 1 << 20) : IUtf8MemoryAllowance
    {
        internal ulong Used;
        public bool TryReserve(ulong bytes) { if (bytes > capacity - Used) return false; Used += bytes; return true; }
        public void Release(ulong bytes) { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}

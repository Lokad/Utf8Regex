using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8FiniteLiteralFamilyAdmissionTests
{
    private static Utf8ResourcePolicy Small => new()
        { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };

    [Theory]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void CompleteFamiliesNeedNoVmScratchAndValidateTheWholeSubject(int count)
    {
        string[] literals = ["猫猫猫", "狗狗狗", "猴猴猴", "兔兔兔", "马马马", "羊羊羊", "虎虎虎", "牛牛牛"];
        var pattern = string.Join('|', literals.Take(count));
        var text = "😀é " + string.Join(' ', literals.Take(count)) + " 猫猫猫猫猫猫";
        var input = Encoding.UTF8.GetBytes(text);
        var storage = new byte[input.Length + 13];
        input.CopyTo(storage.AsSpan(5));
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Small);
            Assert.Equal(new Regex(pattern, options).Count(text), regex.Count(storage.AsSpan(5, input.Length), token.Token));
            Assert.Throws<ArgumentException>(() => regex.Count([.. input, 0xff], token.Token));
        }
    }

    [Theory]
    [InlineData("cat|catalog|dog|emu|fox", "catalog cat dog emu fox")]
    [InlineData("catalog|cat|dog|emu|fox", "catalogcat dog emu fox")]
    [InlineData("😀|😀😀|中|é|東京", "😀😀中é東京")]
    [InlineData("😀😀|😀|中|é|東京", "😀😀中é東京")]
    [InlineData("(cat)|(dog)|(emu)|(fox)", "cat dog emu fox")]
    public void OrderedOverlapsAndDiscardedCapturesKeepTheirCount(string pattern, string text)
    {
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Small);
            Assert.Equal(new Regex(pattern, options).Count(text), regex.Count(Encoding.UTF8.GetBytes(text), token.Token));
        }
    }

    [Fact]
    public void LongNeedlesRetainBoundedGenericComparison()
    {
        var literals = Enumerable.Range(0, 5).Select(i => (char)('a' + i) + new string('x', 1025) + "z").ToArray();
        var pattern = string.Join('|', literals);
        var text = "é😀" + literals[4] + " " + literals[1] + "xx";
        var regex = new Utf8Regex(pattern, RegexOptions.Compiled, Regex.InfiniteMatchTimeout, Small);
        Assert.Equal(new Regex(pattern).Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Theory]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void OtherOptionsKeepTheirScalarOutcomes(RegexOptions options)
    {
        const string pattern = "cat|catalog|dog|emu|fox";
        const string text = "CAT catalog cat dog emu fox";
        var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        Assert.Equal(new Regex(pattern, options).Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void Utf16OffsetRemainsDerivedFromTheOriginalSubject()
    {
        const string pattern = "猫猫猫|狗狗狗|猴猴猴|兔兔兔|马马马";
        const string text = "😀猫猫猫 狗狗狗 猴猴猴";
        var regex = new Utf8Regex(pattern, RegexOptions.Compiled, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        using var token = new CancellationTokenSource();
        Assert.Equal(3, regex.CountFromUtf16Offset(Encoding.UTF8.GetBytes(text), 2, token.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.CountFromUtf16Offset(Encoding.UTF8.GetBytes(text), 1, token.Token));
        var denied = new Utf8Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.CountFromUtf16Offset(Encoding.UTF8.GetBytes(text), 2)).ResourceKind);
    }

    [Fact]
    public void NullableFamiliesKeepScalarEmptyProgression()
    {
        var regex = new Utf8Regex("cat|dog|emu|fox|x*", RegexOptions.Compiled, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        Assert.Equal(4, regex.Count("cat😀xx"u8));
    }

    [Fact]
    public void CallerFundedCountRefundsItsScopeWithoutVmScratch()
    {
        var regex = new Utf8Regex("猫猫猫|狗狗狗|猴猴猴|兔兔兔|马马马", RegexOptions.Compiled, Regex.InfiniteMatchTimeout, Small);
        var allowance = new Account();
        using var token = new CancellationTokenSource();
        Assert.Equal(2, regex.Count("😀猫猫猫狗狗狗"u8, token.Token, allowance));
        Assert.Equal(0UL, allowance.Used);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeWindowsKeepOriginalInterruptionAndWorkDenial(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        const string pattern = "cat|dog|emu|fox|yak";
        var regex = new Utf8Regex(pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(20), Small);
        var input = Encoding.ASCII.GetBytes(new string('x', 262144) + "cat");
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 256) return;
            entered = true;
            if (expire) ticks = 50;
            else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(pattern, Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, token.Token)).Pattern);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered);
        ticks = 0;
        Assert.Equal(1, regex.Count("cat"u8));
        var denied = new Utf8Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input)).ResourceKind);
    }

    [Theory]
    [InlineData(2037)] [InlineData(2038)] [InlineData(2042)] [InlineData(2043)] [InlineData(4095)]
    public void PreparedFamiliesKeepBorrowedWindowLookaheadAndTruncatedTails(int prefixBytes)
    {
        string[] literals = ["Sawyer", "Finn", "Tom", "Huckleberry"];
        var input = Encoding.UTF8.GetBytes("é😀" + new string('x', prefixBytes - 6) +
            "Huckleberry/Tom/Finn/Sawyer/Huckleberr");
        var backing = new byte[input.Length + 6];
        input.CopyTo(backing.AsSpan(5));
        backing[^1] = (byte)'y'; // The last needle's missing byte is outside the borrowed span.
        using var token = new CancellationTokenSource();
        foreach (var policy in Enum.GetValues<Utf8InputValidation>())
        foreach (var count in new[] { 3, 4 })
        {
            var regex = new Utf8Regex(string.Join('|', literals.Take(count)), RegexOptions.Compiled,
                Regex.InfiniteMatchTimeout, Small, inputValidation: policy);
            Assert.Equal(count, regex.Count(backing.AsSpan(5, input.Length), token.Token));
        }
    }

    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void PreparedFamilyBucketOrderKeepsOriginalBranchIdentity(int count)
    {
        string[] literals = ["Sawyer", "Finn", "Tom", "Huckleberry"];
        var regex = new Utf8Regex(string.Join('|', literals.Take(count)));
        var input = Encoding.ASCII.GetBytes(new string('x', 2040) + literals[count - 1]);
        using var token = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, token.Token);
        var breach = Utf8SearchExecutor.TryFindLiteralFamily(regex.Inspection.SearchPlan, input, 0, input.Length,
            false, ref invocation, out var match, out var found);
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.True(found);
        Assert.Equal((2040, literals[count - 1].Length, count - 1), (match.Index, match.Length, match.LiteralId));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PreparedFamilySearchKeepsOriginalInterruptionAfterTrustedAdmission(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        const string pattern = "Sawyer|Finn|Tom|Huckleberry";
        var regex = new Utf8Regex(pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(20), Small,
            inputValidation: Utf8InputValidation.AssumeValid);
        var input = Encoding.ASCII.GetBytes(new string('x', 262144) + "Tom");
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
        Assert.Equal(1, regex.Count("Tom"u8));
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used;
        public bool TryReserve(ulong bytes)
        {
            if (bytes > 1 - Used) return false;
            Used += bytes;
            return true;
        }
        public void Release(ulong bytes)
        {
            Assert.InRange(bytes, 0UL, Used);
            Used -= bytes;
        }
    }
}

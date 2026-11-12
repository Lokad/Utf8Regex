using System.Globalization;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BudgetedCategoryCountTests
{
    [Fact]
    public void NativeMathCountSharesFiniteAuthorityWithoutScratch()
    {
        const string prefix = "é!";
        var text = string.Concat(Enumerable.Repeat("Aé中∑𝛁😀+9\u0378 ", 512));
        var input = Encoding.UTF8.GetBytes(prefix + text);
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var expected = text.EnumerateRunes().Count(rune => Rune.GetUnicodeCategory(rune) == UnicodeCategory.MathSymbol);
        using var token = new CancellationTokenSource();
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = Utf8Pcre2Regex.Create(@"\p{Sm}", default, default,
                new() { MaxWorkSteps = 256, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 },
                Timeout.InfiniteTimeSpan, validation);
            var allowance = new RejectStorage();
            Assert.Equal(expected, regex.Count(input, floor, default, token.Token, allowance));
            Assert.Equal(0, allowance.Attempts);
            if (validation == Utf8InputValidation.Validate)
                Assert.Throws<ArgumentException>(() => regex.Count([.. input, 0xff], floor));
        }
    }

    [Fact]
    public void CategoryCountKeepsRealWorkDenialAndOriginalCancellation()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("a+∑😀", 1 << 16)));
        var limited = Utf8Pcre2Regex.Create(@"\p{Sm}", default, default,
            new() { MaxWorkSteps = 1 }, Timeout.InfiniteTimeSpan, Utf8InputValidation.AssumeValid);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => limited.Count(input)).ErrorKind);
        using var token = new CancellationTokenSource(); token.Cancel();
        var unlimited = new Utf8Pcre2Regex(@"\p{Sm}", inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            unlimited.Count(input, 0, default, token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CategoryCountChecksOriginalInterruptionBetweenBlocks(bool cancel)
    {
        var regex = new Utf8Pcre2Regex(@"\p{Sm}");
        var input = Utf8ValidatedInput.Create(Encoding.ASCII.GetBytes(new string('+', 16384)));
        var reads = 0;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads < 4) return 0; // Origin, admission, first block, second block.
            if (cancel) { source.Cancel(); return 0; }
            return 20;
        });
        var invocation = new Utf8SliceInvocation(default, TimeSpan.FromMilliseconds(20), source.Token);
        Exception? failure = null;
        try { Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input, new(0), default, ref invocation, out _); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        // The public CountCore boundary translates this internal expiry to Pcre2ErrorKind.Timeout.
        else Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        Assert.Equal(4, reads);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    private sealed class RejectStorage : IUtf8MemoryAllowance
    {
        internal int Attempts;
        public bool TryReserve(ulong bytes) { Attempts++; return false; }
        public void Release(ulong bytes) => throw new InvalidOperationException("No loan was admitted.");
    }
}

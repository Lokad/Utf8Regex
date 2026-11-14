using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WordRunCountTests
{
    [Theory]
    [InlineData(@"\b\w{10,}\b")]
    [InlineData(@"\w{10,}")]
    [InlineData(@"(?:\b)(?:[\w]){10,}(?:\b)")]
    [InlineData(@"(\b\w{10,}\b)")]
    [InlineData(@"\w+")]
    [InlineData(@"(\w{2,})")]
    public void CompleteWordProofCountsRunsAcrossBlocks(string pattern)
    {
        var text = "short 123456789 1234567890 _abcdEFGHij " + new string('z', 31) + " " + new string('a', 32) + " " + new string('_', 33) + " !";
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase })
        {
            var regex = new Utf8Regex(pattern, options); var input = Encoding.UTF8.GetBytes(text);
            var expected = new Regex(pattern, options).Count(text);
            var diagnostic = regex.CollectCountDiagnostics(input);
            Assert.Equal(expected, diagnostic.Result); Assert.Equal("fallback_direct_utf8_word_run", diagnostic.ExecutionRoute);
            Assert.Equal(expected, regex.Count(input)); Assert.Equal(expected, regex.Count(input, CancellationToken.None));
        }
    }

    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(10)]
    public void AsciiMembershipAndMinimumsAgreeWithIndependentRegex(int minimum)
    {
        var random = new Random(713); var characters = new char[517];
        foreach (var bounded in new[] { false, true })
        {
            var repeat = @"\w{" + minimum + ",}"; var pattern = bounded ? @"\b" + repeat + @"\b" : repeat;
            var regex = new Utf8Regex(pattern); var oracle = new Regex(pattern);
            for (var trial = 0; trial < 64; trial++)
            {
                for (var i = 0; i < characters.Length; i++) characters[i] = (char)random.Next(128);
                var text = new string(characters) + " abcdefghijklmnopqrstuvwxyz";
                Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
            }
        }
    }

    [Theory]
    [InlineData(@"\w{10,}?")]
    [InlineData(@"\b\w{10,}?\b")]
    [InlineData(@"\w{0,}")]
    [InlineData(@"\w{10,20}")]
    [InlineData(@"[\w-]{10,}")]
    [InlineData(@"\B\w{10,}\B")]
    [InlineData(@"\b\w{10,}\b!")]
    [InlineData(@"\w{10,2147483647}")]
    public void UnprovedShapesKeepTheirOwnedSelection(string pattern)
    {
        const string text = "abcdefghijklmnopqrstuvwx abcdefghijk-abcdefghijk!";
        var regex = new Utf8Regex(pattern); var diagnostic = regex.CollectCountDiagnostics(Encoding.UTF8.GetBytes(text));
        Assert.Equal(new Regex(pattern).Count(text), diagnostic.Result);
        Assert.NotEqual("fallback_direct_utf8_word_run", diagnostic.ExecutionRoute);
    }

    [Theory] [InlineData(RegexOptions.RightToLeft)] [InlineData(RegexOptions.NonBacktracking)] [InlineData(RegexOptions.ECMAScript)]
    public void DirectionAndOtherExecutionContractsRetainOwnedBehavior(RegexOptions options)
    {
        const string pattern = @"\b\w{10,}\b"; const string text = "abc abcdefghijkl abcdefghijklmno";
        var regex = new Utf8Regex(pattern, options); var diagnostic = regex.CollectCountDiagnostics(Encoding.UTF8.GetBytes(text));
        Assert.Equal(new Regex(pattern, options).Count(text), diagnostic.Result);
        Assert.NotEqual("fallback_direct_utf8_word_run", diagnostic.ExecutionRoute);
    }

    [Fact]
    public void WiderScalarsRetainScalarMembershipAndOriginalAuthority()
    {
        const string pattern = @"\b\w{10,}\b";
        var texts = new[] { new string('é', 10), string.Concat(Enumerable.Repeat("𐐀", 10)), "😀 " + new string('a', 10) + " 😀" };
        foreach (var text in texts)
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options); var diagnostic = regex.CollectCountDiagnostics(Encoding.UTF8.GetBytes(text));
            Assert.Equal(1, diagnostic.Result); Assert.Equal("fallback_direct_utf8_word_run", diagnostic.ExecutionRoute);
        }
    }

    [Theory]
    [InlineData(@"\w{2,}", "éé\u200céé", 2)]
    [InlineData(@"\b\w{2,}\b", "éé\u200céé", 0)]
    [InlineData(@"\b\w{2,}\b", "\u200dété", 0)]
    [InlineData(@"\b\w{2,}\b", "été\u200d ", 0)]
    [InlineData(@"\b\w{2,}\b", "é\u0301", 1)]
    [InlineData(@"\b\w{2,}\b", "é\u0903é", 0)]
    [InlineData(@"\b\w{2,}\b", "𐐀𐐁", 1)]
    [InlineData(@"\w{2,}", "𝟙𝟚", 1)]
    [InlineData(@"(\w{2,})", "éé 𐐀𐐁 😀 a", 2)]
    public void CompleteScalarRunsKeepJoinerBoundariesMarksAndSupplementaryWidths(
        string pattern, string text, int expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant })
        foreach (var policy in new[] { Utf8InputValidation.Validate, Utf8InputValidation.AssumeValid })
        {
            var regex = new Utf8Regex(pattern, options, inputValidation: policy);
            Assert.Equal(expected, regex.Count(input));
            Assert.Equal(expected, new Utf8Regex(pattern, RegexOptions.NonBacktracking).Count(input));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(28)] [InlineData(29)]
    [InlineData(30)] [InlineData(31)] [InlineData(32)] [InlineData(33)]
    [InlineData(63)] [InlineData(64)] [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void ScalarCutsAndBorrowedOffsetsPreserveWordRuns(int padding)
    {
        var text = new string('!', padding) + new string('é', 11) + " " +
            string.Concat(Enumerable.Repeat("𐐀", 11)) + " a\u200cbb";
        var input = Encoding.UTF8.GetBytes(text);
        byte[] storage = [0xff, 0xff, 0xff, .. input, 0xff];
        foreach (var pattern in new[] { @"\w{10,}", @"\b(\w{10,})\b" })
        foreach (var policy in new[] { Utf8InputValidation.Validate, Utf8InputValidation.AssumeValid })
            Assert.Equal(2, new Utf8Regex(pattern, inputValidation: policy).Count(storage.AsSpan(3, input.Length)));
    }

    [Fact]
    public void MixedMembershipAndBoundariesAgreeWithIndependentExecution()
    {
        string[] alphabet = ["a", "9", "_", "é", "中", "𐐀", "𝟙", "\u0301", "\u0903", "\u200c", "\u200d", "!", "😀", "\0"];
        var random = new Random(92713);
        foreach (var pattern in new[] { @"\w+", @"\w{2,}", @"\b\w{2,}\b", @"\b(\w{10,})\b" })
        {
            var regex = new Utf8Regex(pattern);
            var independent = new Utf8Regex(pattern, RegexOptions.NonBacktracking);
            for (var trial = 0; trial < 64; trial++)
            {
                var text = string.Concat(Enumerable.Range(0, 100).Select(_ => alphabet[random.Next(alphabet.Length)]));
                var input = Encoding.UTF8.GetBytes(text);
                Assert.Equal(independent.Count(input), regex.Count(input));
                // BMP-only text also has an independent BCL matching-unit oracle.
                var bmp = string.Concat(text.EnumerateRunes().Where(static rune => rune.IsBmp).Select(static rune => rune.ToString()));
                Assert.Equal(Regex.Count(bmp, pattern), regex.Count(Encoding.UTF8.GetBytes(bmp)));
            }
        }
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void MixedCountNeedsNoVmScratchAndPreservesCallerFunding(Utf8InputValidation policy)
    {
        const string pattern = @"\b(\w{10,})\b";
        var input = Encoding.UTF8.GetBytes(new string('é', 10) + " " + string.Concat(Enumerable.Repeat("𐐀", 10)));
        var regex = new Utf8Regex(pattern, resourcePolicy: new() { MaxWorkSteps = 100000,
            MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 }, inputValidation: policy);
        var account = new Account();
        Assert.True(account.TryReserve(17));
        Assert.Equal(2, regex.Count(input));
        Assert.Equal(2, regex.Count(input, default, account));
        Assert.Equal(17UL, account.Used);
        var denied = new Utf8Regex(pattern, resourcePolicy: new() { MaxWorkSteps = 1 }, inputValidation: policy);
        Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input, default, account));
        Assert.Equal(17UL, account.Used);
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token, account)).CancellationToken);
        Assert.Equal(17UL, account.Used);
        account.Release(17); Assert.Equal(0UL, account.Used);
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used;
        public bool TryReserve(ulong bytes) { if (bytes > 17 - Used) return false; Used += bytes; return true; }
        public void Release(ulong bytes) { Assert.True(bytes <= Used); Used -= bytes; }
    }

    [Fact]
    public void UsefulFinitePoliciesAdmitTheConstantSpaceCounter()
    {
        var regex = new Utf8Regex(@"\b\w{10,}\b", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        using var token = new CancellationTokenSource();
        Assert.Equal(2, regex.Count("abcdefghijkl abcdefghijklmno"u8, token.Token));
        Assert.Equal("fallback_direct_utf8_word_run", regex.CollectCountDiagnostics("abcdefghijkl"u8).ExecutionRoute);
        var tooLittle = new Utf8Regex(@"\b\w{10,}\b", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => tooLittle.Count(new byte[4096]));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
    }

    [Theory] [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void NativeWorkObservesOriginalCancellationAndDeadline(bool timeout, bool wider)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var canceled = new CancellationTokenSource();
        var regex = new Utf8Regex(@"\b\w{10,}\b", RegexOptions.None, timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(wider ? "éééééééééé𐐀 " : "abcdefghijkl ", 1024))); var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (Utf8SearchDiagnosticsSession.Current!.ExecutionRoute != Utf8ExecutionRoute.FallbackDirectUtf8WordRun) return;
            reached = true; if (timeout) ticks = 100; else canceled.Cancel();
        });
        try
        {
            var failure = Record.Exception(() => regex.Count(input, canceled.Token)); Assert.True(reached);
            if (timeout) { var error = Assert.IsType<RegexMatchTimeoutException>(failure); Assert.Empty(error.Input); Assert.Equal(regex.Pattern, error.Pattern); }
            else Assert.Equal(canceled.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void FullSubjectValidationAndPreCancellationRemainAuthoritative()
    {
        var regex = new Utf8Regex(@"\b\w{10,}\b");
        var malformed = Encoding.UTF8.GetBytes("abcdefghijkl ").Concat(new byte[] { 0xf0, 0x9f }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(malformed, token.Token)).CancellationToken);
        Assert.Equal(0, regex.Count(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void NativeCounterCannotOutrunFiniteWorkAfterValidation()
    {
        const string pattern = @"\b\w{10,}\b";
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abcdefghijkl ", 1024))); ulong firstNative = 0;
        var reference = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000 });
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (firstNative == 0 && Utf8SearchDiagnosticsSession.Current!.ExecutionRoute == Utf8ExecutionRoute.FallbackDirectUtf8WordRun) firstNative = steps;
        });
        try { Assert.Equal(1024, reference.Count(input)); } finally { session.Complete(); }
        Assert.True(firstNative > 1);
        var limited = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = firstNative - 1 });
        session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Count(input));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
            Assert.Equal(Utf8ExecutionRoute.FallbackDirectUtf8WordRun, session.ExecutionRoute);
        }
        finally { session.Complete(); }
    }
}

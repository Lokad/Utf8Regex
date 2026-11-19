using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiClassRunCountTests
{
    [Fact]
    public void NullableTerminalCountHonorsOffsetAndNotEmpty()
    {
        using var regex = new Utf8Pcre2Regex(@"[^\r\n]*");
        using var token = new CancellationTokenSource();
        var input = "é\r\n𐐀\n"u8;
        Assert.Equal(6, regex.Count(input, cancellationToken: token.Token));
        Assert.Equal(3, regex.Count(input, 4, cancellationToken: token.Token));
        Assert.Equal(1, regex.Count(input, input.Length, cancellationToken: token.Token));
        Assert.Equal(2, regex.Count(input, matchOptions: Pcre2MatchOptions.NotEmpty, cancellationToken: token.Token));
    }

    [Theory]
    [InlineData("[a-z]{10,}")]
    [InlineData("[a-zA-Z0-9]{10,}")]
    [InlineData("[aba-z]{10,}")]
    public void PositiveAsciiRangesNeedNoScratchOnMixedText(string pattern)
    {
        foreach (var length in new[] { 4095, 4096, 4097 })
        {
            var text = "α" + new string('a', length) + "😀shortéabcdefghij中";
            var input = Encoding.UTF8.GetBytes("pad" + text + "pad");
            var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Ucp, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 100000,
                    MaxDepth = 1,
                    MaxWorkingBytes = 1,
                    MaxResultBytes = 1
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            });
            using var token = new CancellationTokenSource();
            var allowance = new RejectStorage();
            var subject = input.AsSpan(3, input.Length - 6);
            Assert.Equal(2, regex.Count(subject, 0, default, token.Token, allowance));
            Assert.Equal(1, regex.Count(subject, 2 + length, default, token.Token, allowance));
            Assert.Equal(0, regex.Count(subject, subject.Length, default, token.Token, allowance));
            Assert.Equal(0, allowance.Attempts);
        }
    }

    [Theory]
    [InlineData("[a-zA-Z0-9]{2,}")]
    [InlineData(@"[a-z\0]{2,}")]
    [InlineData("[a-ba-c]{2,}")]
    [InlineData("[a-zA-Z0-9]++")]
    public void RangeUnionsKeepCountsAtEveryScalarFloorIncludingNulAndTails(string pattern)
    {
        const string text = "αabZ09\0!😀01zzéAB\0中";
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var bytes = storage.AsSpan(3, storage.Length - 6);
        {
            // Possessive syntax has its existing atomic runner and admission;
            // this change only proves the complete greedy run's byte counter.
            var atomic = pattern.EndsWith("++", StringComparison.Ordinal);
            var regex = new Utf8Pcre2Regex(pattern, options: default, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 100000,
                    MaxDepth = 1024,
                    MaxWorkingBytes = atomic ? 1UL << 20 : 1,
                    MaxResultBytes = 1
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            });
            // Ordered and longest choices agree for these complete greedy runs.
            var oracle = new System.Text.RegularExpressions.Regex(pattern.Replace("++", "+"),
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            for (var floor = 0; floor <= text.Length; floor++)
            {
                if (floor < text.Length && char.IsLowSurrogate(text[floor])) continue;
                Assert.Equal(oracle.Matches(text, floor).Count,
                    regex.Count(bytes, Encoding.UTF8.GetByteCount(text.AsSpan(0, floor))));
            }
        }
    }

    [Fact]
    public void LargeUnionKeepsBoundedWorkWithoutWorkspace()
    {
        var pattern = "[" + string.Concat(Enumerable.Repeat("a-zA-Z0-9", 43)) + "]+";
        var bounded = new Utf8Pcre2Regex(pattern, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 2,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => bounded.Count("a"u8)).ErrorKind);
        var admitted = new Utf8Pcre2Regex(pattern, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var allowance = new RejectStorage();
        Assert.Equal(1, admitted.Count("a"u8, 0, default, default, allowance));
        Assert.Equal(0, admitted.Count([], 0, default, default, allowance));
        Assert.Equal(0, allowance.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LargeUnionPreparationKeepsTheOriginalInterruptionAuthority(bool cancel)
    {
        var large = new Utf8Pcre2Regex("[" + string.Concat(Enumerable.Repeat("a-zA-Z0-9", 43)) + "]+", policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var reads = 0;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            // Interrupt at a later checkpoint under the original invocation.
            if (++reads < 4) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 50;
        });
        var failure = Record.Exception(() => large.Count("a"u8, 0, default, source.Token));
        Assert.Equal(4, reads);
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }

    [Fact]
    public void AsciiProjectionDoesNotProveAUnicodeOrNegatedClass()
    {
        Assert.Equal(1, new Utf8Pcre2Regex("[a-zé]+").Count("abcédef"u8));
        Assert.Equal(1, new Utf8Pcre2Regex("[^0-9]+").Count("aé😀b"u8));
        Assert.Equal(1, new Utf8Pcre2Regex(@"[\w]{2,}", options: Pcre2CompileOptions.Ucp).Count("ééé"u8));
        Assert.Equal(1, new Utf8Pcre2Regex("[a-z]+", options: Pcre2CompileOptions.Caseless).Count("aKb"u8));
    }

    [Theory]
    [InlineData(@"\w{10,}", "short words only", 0)]
    [InlineData(@"\w{3,}", "ab abc abcd x", 2)]
    [InlineData(@"[a-z]{2,}", "abc!!def!g", 2)]
    [InlineData(@"[^a]{2,}", "bbaccaad", 2)]
    [InlineData(@"[a\0]{2,}", "a\0!!aa", 2)]
    [InlineData(@"[a-z]*", "ab!", 3)]
    [InlineData(@"[a-z]{2,}?", "abcdef", 3)]
    [InlineData(@"[a-z]{2,3}", "abcdefg", 2)]
    [InlineData(@"([a-z]{2,3})", "abcdefg", 2)]
    [InlineData(@"([a-z]){2,3}", "abcdefg", 2)]
    [InlineData(@"(?i)[a-z]{3,}", "ABC xyz", 2)]
    [InlineData(@"\w{3,}", "éabcd😀xyz", 2)]
    public void CompleteClassRunsAndExcludedFormsKeepTheirPublicCounts(string pattern, string text, int expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.Equal(expected, regex.Count(input));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input, 0, Pcre2MatchOptions.None, token.Token));
    }

    [Fact]
    public void MemberRunsCrossBlocksAndRespectOffsetsWithoutPublishingRanges()
    {
        var regex = new Utf8Pcre2Regex(@"\w{10,}");
        var text = new string('a', 4095) + " " + new string('b', 9) + " " +
            new string('c', 10) + "\0" + new string('d', 8200);
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = storage.AsSpan(3, text.Length);
        Assert.Equal(3, regex.Count(input));
        Assert.Equal(2, regex.Count(input, 4095));
        Assert.Equal(0, regex.Count(input, input.Length - 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("éabcd"u8, 1));
    }

    [Fact]
    public void UnicodePropertiesAndAnchoringKeepTheirOwnRules()
    {
        var unicode = new Utf8Pcre2Regex(@"\w{3,}", options: Pcre2CompileOptions.Ucp);
        Assert.Equal(1, unicode.Count("ééé"u8));
        Assert.Equal(2, unicode.Count("abc def"u8));
        var plain = new Utf8Pcre2Regex(@"[a-z]{2,}");
        Assert.Equal(0, plain.Count("0abc xyz"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.Equal(1, plain.Count("abc zz"u8, 0, Pcre2MatchOptions.EndAnchored));
        var anchored = new Utf8Pcre2Regex(@"[a-z]{2,}", options: Pcre2CompileOptions.Anchored);
        Assert.Equal(0, anchored.Count("0abc xyz"u8));
        var crlf = new Utf8Pcre2Regex(@"[a-z]{2,}", options: default, compileSettings: new() { Newline = Pcre2NewlineConvention.Crlf }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(2, crlf.Count("\r\nabc def\r\n"u8));
    }

    [Fact]
    public void CapturedCountNeedsNoScratchWhileCapturesAndMatchOptionsRemain()
    {
        const string pattern = "([a-z]){2,3}";
        var regex = new Utf8Pcre2Regex(pattern, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = "éabcdefg 😀 ab"u8.ToArray();
        using var token = new CancellationTokenSource();
        var allowance = new RejectStorage();
        Assert.Equal(3, regex.Count(input));
        Assert.Equal(2, regex.Count(input, 5, default, token.Token, allowance));
        Assert.Equal(0, allowance.Attempts);
        var values = new Utf8Pcre2Regex(pattern);
        ReadOnlySpan<byte> retained1Input = input;
        var retained1Captures = new Utf8Pcre2CaptureRange[values.CaptureGroupCount + 1];
        var retained = values.Match(retained1Input, captures: retained1Captures);
        Assert.Equal("c", System.Text.Encoding.UTF8.GetString((retained1Captures[1].Success ? retained1Input.Slice(retained1Captures[1].StartOffsetInBytes, retained1Captures[1].EndOffsetInBytes - retained1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(3, values.Count(input));
        Assert.Equal("c", System.Text.Encoding.UTF8.GetString((retained1Captures[1].Success ? retained1Input.Slice(retained1Captures[1].StartOffsetInBytes, retained1Captures[1].EndOffsetInBytes - retained1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(0, values.Count(input, 0, Pcre2MatchOptions.Anchored, token.Token));
    }

    [Theory]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    public void ControlledRunsNeedNoWorkingLoanOrResultStorage(int length)
    {
        var regex = new Utf8Pcre2Regex(@"[a-z]{10,}", options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100_000,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var storage = Encoding.ASCII.GetBytes("pad" + new string('a', length) + "!short!abcdefghij!pad");
        using var source = new CancellationTokenSource();
        var allowance = new RejectStorage();
        Assert.Equal(2, regex.Count(storage.AsSpan(3, storage.Length - 6), 0, default, source.Token, allowance));
        Assert.Equal(1, regex.Count(storage.AsSpan(3, storage.Length - 6), length, default, source.Token, allowance));
        Assert.Equal(0, regex.Count([], 0, default, source.Token, allowance));
        Assert.Equal(0, allowance.Attempts);
    }

    [Theory]
    [InlineData("[a-z]+")]
    [InlineData("[a-zA-Z0-9]+")]
    public void WorkAdmissionStillBoundsLongRunsAndDenseCandidates(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(1, regex.Count("abc"u8));
        foreach (var text in new[] { new string('a', 1 << 20), string.Concat(Enumerable.Repeat("a!", 4096)) })
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
                regex.Count(bytes)).ErrorKind);
            // Also exercise the global driver with its own invocation budget.
            var input = Utf8ValidatedInput.BorrowBytes(bytes);
            var invocation = new Utf8SliceInvocation(new() { MaxWorkSteps = 100 }, Timeout.InfiniteTimeSpan, default);
            Pcre2MatchException? failure = null;
            try { Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input, new(0), default, ref invocation, out _); }
            catch (Pcre2MatchException error) { failure = error; }
            Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EmptyStreamCannotIgnoreAnExistingSharedDenial(int kind)
    {
        var regex = new Utf8Pcre2Regex(@"[a-z]+");
        var invocation = new Utf8SliceInvocation(new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1 },
            Timeout.InfiniteTimeSpan, default);
        if (kind == 0) { invocation.TryChargeStep(); invocation.TryChargeStep(); }
        else if (kind == 1) invocation.Budget.TryNoteDepth(2);
        else invocation.TryReserveWorking(2);
        var input = Utf8ValidatedInput.BorrowBytes([]);
        Pcre2MatchException? failure = null;
        try { Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input, new(0), default, ref invocation, out _); }
        catch (Pcre2MatchException error) { failure = error; }
        Assert.Equal(kind switch
        {
            0 => Pcre2ErrorKind.MatchLimit,
            1 => Pcre2ErrorKind.DepthLimit,
            _ => Pcre2ErrorKind.HeapLimit
        }, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("[a-z]+", false, false)]
    [InlineData("[a-z]+", false, true)]
    [InlineData("[a-z]+", true, false)]
    [InlineData("[a-z]+", true, true)]
    [InlineData("[a-zA-Z0-9]+", false, false)]
    [InlineData("[a-zA-Z0-9]+", false, true)]
    [InlineData("[a-zA-Z0-9]+", true, false)]
    [InlineData("[a-zA-Z0-9]+", true, true)]
    public void ControlledCountKeepsOriginalInterruptionThroughPublication(string pattern, bool cancel, bool atPublication)
    {
        var armed = false; var reads = 0; var interruptAt = int.MaxValue;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (!armed || ++reads < interruptAt) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 20;
        });
        var regex = new Utf8Pcre2Regex(pattern, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var input = Encoding.ASCII.GetBytes(new string('a', 65536));
        armed = true;
        Assert.Equal(1, regex.Count(input, 0, default, source.Token));
        interruptAt = atPublication ? reads : reads / 2;
        Assert.True(interruptAt > 2);
        reads = 0;
        var failure = Record.Exception(() => regex.Count(input, 0, default, source.Token));
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ControlledStreamChecksInterruptionBetweenBlocks(bool cancel)
    {
        var regex = new Utf8Pcre2Regex(@"[a-z]+", policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var input = Encoding.ASCII.GetBytes(new string('a', 16384));
        var reads = 0;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads < 4) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 20;
        });
        Exception? failure = null;
        try { _ = regex.Count(input, 0, default, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.True(reads >= 4);
    }

    private sealed class RejectStorage : IUtf8MemoryAllowance
    {
        internal int Attempts;
        public bool TryReserve(ulong bytes) { Attempts++; return false; }
        public void Release(ulong bytes) => throw new InvalidOperationException("No storage was admitted.");
    }
}

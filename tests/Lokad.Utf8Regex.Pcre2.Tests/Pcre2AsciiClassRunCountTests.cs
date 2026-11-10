using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiClassRunCountTests
{
    [Theory]
    [InlineData(@"\w{10,}", "short words only", 0)]
    [InlineData(@"\w{3,}", "ab abc abcd x", 2)]
    [InlineData(@"[a-z]{2,}", "abc!!def!g", 2)]
    [InlineData(@"[^a]{2,}", "bbaccaad", 2)]
    [InlineData(@"[a\0]{2,}", "a\0!!aa", 2)]
    [InlineData(@"[a-z]*", "ab!", 3)]
    [InlineData(@"[a-z]{2,}?", "abcdef", 3)]
    [InlineData(@"[a-z]{2,3}", "abcdefg", 2)]
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
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { (byte)'a', 255 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("éabcd"u8, 1));
    }

    [Fact]
    public void UnicodePropertiesAndAnchoringKeepTheirOwnRules()
    {
        var unicode = new Utf8Pcre2Regex(@"\w{3,}", Pcre2CompileOptions.Ucp);
        Assert.Equal(1, unicode.Count("ééé"u8));
        Assert.Equal(2, unicode.Count("abc def"u8));
        var plain = new Utf8Pcre2Regex(@"[a-z]{2,}");
        Assert.Equal(0, plain.Count("0abc xyz"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.Equal(1, plain.Count("abc zz"u8, 0, Pcre2MatchOptions.EndAnchored));
        var anchored = new Utf8Pcre2Regex(@"[a-z]{2,}", Pcre2CompileOptions.Anchored);
        Assert.Equal(0, anchored.Count("0abc xyz"u8));
        var crlf = new Utf8Pcre2Regex(@"[a-z]{2,}", default,
            new() { Newline = Pcre2NewlineConvention.Crlf }, default, Timeout.InfiniteTimeSpan);
        Assert.Equal(2, crlf.Count("\r\nabc def\r\n"u8));
    }

    [Theory]
    [InlineData(4095)] [InlineData(4096)] [InlineData(4097)]
    public void ControlledRunsNeedNoWorkingLoanOrResultStorage(int length)
    {
        var regex = Utf8Pcre2Regex.Create(@"[a-z]{10,}", default, default,
            new() { MaxWorkSteps = 100_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 },
            Timeout.InfiniteTimeSpan);
        var storage = Encoding.ASCII.GetBytes("pad" + new string('a', length) + "!short!abcdefghij!pad");
        using var source = new CancellationTokenSource();
        var allowance = new RejectStorage();
        Assert.Equal(2, regex.Count(storage.AsSpan(3, storage.Length - 6), 0, default, source.Token, allowance));
        Assert.Equal(1, regex.Count(storage.AsSpan(3, storage.Length - 6), length, default, source.Token, allowance));
        Assert.Equal(0, regex.Count([], 0, default, source.Token, allowance));
        Assert.Equal(0, allowance.Attempts);
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { (byte)'a', 255 }, 0, default, source.Token, allowance));
    }

    [Fact]
    public void WorkAdmissionStillBoundsLongRunsAndDenseCandidates()
    {
        var regex = Utf8Pcre2Regex.Create(@"[a-z]+", default, default,
            new() { MaxWorkSteps = 100 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(1, regex.Count("abc"u8));
        foreach (var text in new[] { new string('a', 1 << 20), string.Concat(Enumerable.Repeat("a!", 4096)) })
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
                regex.Count(bytes)).ErrorKind);
            // Independently reach the stream after validation: otherwise the
            // long input could exhaust its work allowance during validation.
            var input = Utf8ValidatedInput.Create(bytes);
            var invocation = new Utf8SliceInvocation(new() { MaxWorkSteps = 100 }, Timeout.InfiniteTimeSpan, default);
            Pcre2MatchException? failure = null;
            try { Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input, new(0), default, ref invocation, out _); }
            catch (Pcre2MatchException error) { failure = error; }
            Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void EmptyStreamCannotIgnoreAnExistingSharedDenial(int kind)
    {
        var regex = new Utf8Pcre2Regex(@"[a-z]+");
        var invocation = new Utf8SliceInvocation(new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1 },
            Timeout.InfiniteTimeSpan, default);
        if (kind == 0) { invocation.TryChargeStep(); invocation.TryChargeStep(); }
        else if (kind == 1) invocation.Budget.TryNoteDepth(2);
        else invocation.TryReserveWorking(2);
        var input = Utf8ValidatedInput.Create([]);
        Pcre2MatchException? failure = null;
        try { Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input, new(0), default, ref invocation, out _); }
        catch (Pcre2MatchException error) { failure = error; }
        Assert.Equal(kind switch { 0 => Pcre2ErrorKind.MatchLimit, 1 => Pcre2ErrorKind.DepthLimit,
            _ => Pcre2ErrorKind.HeapLimit }, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void ControlledCountKeepsOriginalInterruptionThroughPublication(bool cancel, bool atPublication)
    {
        var armed = false; var reads = 0; var interruptAt = int.MaxValue;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (!armed || ++reads < interruptAt) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 20;
        });
        var regex = Utf8Pcre2Regex.Create(@"[a-z]+", default, default,
            new() { MaxWorkingBytes = 1 }, TimeSpan.FromMilliseconds(20));
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
    [InlineData(false)] [InlineData(true)]
    public void ControlledStreamChecksInterruptionBetweenBlocks(bool cancel)
    {
        var regex = new Utf8Pcre2Regex(@"[a-z]+");
        var input = Utf8ValidatedInput.Create(Encoding.ASCII.GetBytes(new string('a', 16384)));
        var reads = 0;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads < 4) return 0; // Deadline origin, admission, first block, second block.
            if (cancel) { source.Cancel(); return 0; }
            return 20;
        });
        var invocation = new Utf8SliceInvocation(default, TimeSpan.FromMilliseconds(20), source.Token);
        Exception? failure = null;
        try { Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input, new(0), default, ref invocation, out _); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(4, reads);
    }

    private sealed class RejectStorage : IUtf8MemoryAllowance
    {
        internal int Attempts;
        public bool TryReserve(ulong bytes) { Attempts++; return false; }
        public void Release(ulong bytes) => throw new InvalidOperationException("No storage was admitted.");
    }
}

using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CallerCaptureTests
{
    [Theory]
    [InlineData("ab", "πab😀", 2)]
    [InlineData("(?<n>ab)", "πab😀", 2)]
    [InlineData("(?<x>é😀)(?<absent>x)?", "πé😀", 2)]
    [InlineData(@"([a-z]+)-\1", "πabc-abc", 2)]
    [InlineData("(?|(ab)|(a))", "a", 0)]
    [InlineData("(?J)(?<n>a)|(?<n>b)", "b", 0)]
    [InlineData("(?<=(?<prefix>é))(?<value>😀)", "é😀", 2)]
    [InlineData(@"([a-z]+)-\K([0-9]+)", "foo-123", 0)]
    [InlineData("(a)+", "aaa", 0)]
    [InlineData("(a*)", "x", 0)]
    [InlineData("(?<x>needle)", "nothing", 0)]
    public void OutputWidthsKeepEveryExistingCaptureFieldAndUntouchedTail(string pattern, string text, int start)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Pcre2Regex(pattern);
        var expected = regex.MatchDetailed(input, start);
        var count = regex.CaptureGroupCount + 1;
        foreach (var width in new[] { 0, 1, 2, count, count + 2 }.Distinct())
        {
            var output = Enumerable.Repeat(Sentinel, width).ToArray();
            var actual = regex.Match(input, start, output);
            Assert.Equal(expected.Success, actual.Success);
            if (actual.Success)
            {
                Assert.Equal(expected.Value.StartOffsetInBytes, actual.StartOffsetInBytes);
                Assert.Equal(expected.Value.EndOffsetInBytes, actual.EndOffsetInBytes);
                Assert.Equal(expected.Value.IsUtf8SliceWellFormed, actual.IsUtf8SliceWellFormed);
            }
            for (var slot = 0; slot < Math.Min(width, count); slot++)
            {
                if (!expected.Success) { Assert.Equal(default, output[slot]); continue; }
                var group = expected.GetGroup(slot);
                Assert.Equal(group.Number, output[slot].Number);
                Assert.Equal(group.Success, output[slot].Success);
                Assert.Equal(group.StartOffsetInBytes, output[slot].StartOffsetInBytes);
                Assert.Equal(group.EndOffsetInBytes, output[slot].EndOffsetInBytes);
                Assert.Equal(group.IsUtf8SliceWellFormed, output[slot].IsUtf8SliceWellFormed);
            }
            foreach (var tail in output.AsSpan(Math.Min(width, count))) Assert.Equal(Sentinel, tail);
        }
    }

    [Fact]
    public void ByteFragmentsAndNoncontiguousWholeRangesKeepTheirStatus()
    {
        var byteRegex = new Utf8Pcre2Regex(@"(\C)", compileSettings: new() { BackslashC = Pcre2BackslashCPolicy.Allow });
        var output = new Utf8Pcre2CaptureRange[2];
        var first = byteRegex.Match("😀x"u8, captures: output);
        Assert.Equal((0, 1), (first.StartOffsetInBytes, first.EndOffsetInBytes));
        Assert.False(first.IsUtf8SliceWellFormed); Assert.False(output[1].IsUtf8SliceWellFormed);
        Assert.Equal(1, output[1].EndOffsetInBytes);
        var reset = new Utf8Pcre2Regex(@"(?=(ab)\K)", compileSettings: new() { AllowLookaroundBackslashK = true });
        var noncontiguous = reset.Match("ab"u8, captures: output);
        Assert.True(noncontiguous.Success); Assert.False(noncontiguous.HasContiguousByteRange);
        Assert.Equal((2, 0), (output[0].StartOffsetInBytes, output[0].EndOffsetInBytes));
        Assert.False(output[0].IsUtf8SliceWellFormed);
        Assert.Equal((0, 2), (output[1].StartOffsetInBytes, output[1].EndOffsetInBytes));
    }

    [Theory]
    [InlineData("(ab)", "πab", Pcre2CompileOptions.None, 2, Pcre2MatchOptions.Anchored, 2, 4)]
    [InlineData("(a*)", "ba", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.NotEmpty, 1, 2)]
    [InlineData("^(a+)$", "aaa\na", Pcre2CompileOptions.Multiline, 0, Pcre2MatchOptions.NotBol, 4, 5)]
    [InlineData("(ab)", "AB", Pcre2CompileOptions.Caseless, 0, Pcre2MatchOptions.None, 0, 2)]
    public void CompileAndMatchOptionsKeepTheirByteStarts(string pattern, string text, Pcre2CompileOptions options,
        int start, Pcre2MatchOptions flags, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8Pcre2Regex(pattern, options);
        var output = new Utf8Pcre2CaptureRange[2];
        var match = regex.Match(Encoding.UTF8.GetBytes(text), start, output, flags);
        Assert.True(match.Success);
        Assert.Equal((expectedStart, expectedEnd), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal((expectedStart, expectedEnd), (output[1].StartOffsetInBytes, output[1].EndOffsetInBytes));
        Assert.True(output[1].IsUtf8SliceWellFormed);
    }

    [Fact]
    public void CallerSlotsNeedNoResultPayloadAndRefusalClearsOnlyTheirPrefix()
    {
        var regex = Utf8Pcre2Regex.Create("(?<x>a)+", default, default, new()
        {
            MaxWorkSteps = 100000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1
        }, TimeSpan.FromSeconds(10));
        var account = new Account(); var output = Enumerable.Repeat(Sentinel, 3).ToArray();
        var match = regex.Match("aaa"u8, captures: output, workingMemoryAllowance: account);
        Assert.True(match.Success); Assert.Equal(2, output[1].StartOffsetInBytes);
        Assert.Equal(Sentinel, output[2]); Assert.True(account.Attempts > 0); Assert.Equal(0UL, account.Used);
        account.Maximum = 0; var attempts = account.Attempts;
        Assert.Throws<Utf8MemoryAdmissionException>(() => regex.Match("aaa"u8, captures: output, workingMemoryAllowance: account));
        Assert.Equal(attempts + 1, account.Attempts); Assert.Equal(0UL, account.Used);
        Assert.Equal(default, output[0]); Assert.Equal(default, output[1]); Assert.Equal(Sentinel, output[2]);
        Assert.Equal("aaa"u8.ToArray(), match.GetValueBytes().ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInterruptionAfterAcquisitionRefundsAndClearsOutput(bool deadline)
    {
        long now = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(?<x>a)+", matchTimeout: deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan);
        var account = new Account { AfterGrant = () => { if (deadline) now = 101; else source.Cancel(); } };
        var output = Enumerable.Repeat(Sentinel, 3).ToArray();
        if (deadline) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() =>
            regex.Match("aaa"u8, captures: output, cancellationToken: source.Token, workingMemoryAllowance: account)).ErrorKind);
        else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Match("aaa"u8, captures: output, cancellationToken: source.Token, workingMemoryAllowance: account)).CancellationToken);
        Assert.True(account.Attempts > 0); Assert.Equal(0UL, account.Used);
        Assert.Equal(default, output[0]); Assert.Equal(default, output[1]); Assert.Equal(Sentinel, output[2]);
    }

    [Fact]
    public void FundedMatchSurvivesCloseDuringUseAndPreCancellationWinsAfterClose()
    {
        var program = new Account(); var working = new Account();
        using var owner = Utf8Pcre2Regex.Compile("(?<x>a)+", default, default, TimeSpan.FromSeconds(10),
            new() { MaxResultBytes = 1 }, default, Timeout.InfiniteTimeSpan, default, program);
        var held = program.Used; Assert.True(held > 0);
        working.AfterGrant = () => { owner.Dispose(); Assert.Equal(held, program.Used); };
        var output = new Utf8Pcre2CaptureRange[2];
        var result = owner.Match("aaa"u8, captures: output, workingMemoryAllowance: working);
        Assert.Equal((0, 3), (result.StartOffsetInBytes, result.EndOffsetInBytes));
        Assert.Equal(2, output[1].StartOffsetInBytes); Assert.Equal(0UL, program.Used); Assert.Equal(0UL, working.Used);
        var retained = output[1];
        Assert.Throws<ObjectDisposedException>(() => owner.Match("aaa"u8, captures: output));
        Assert.Equal(retained, output[1]);
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
            owner.Match("aaa"u8, -1, output, cancellationToken: source.Token)).CancellationToken);
        Assert.Equal(retained, output[1]); Assert.Equal("aaa"u8.ToArray(), result.GetValueBytes().ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptionDuringPartialPublicationClearsThePrefixAndKeepsExcess(bool deadline)
    {
        using var source = new CancellationTokenSource();
        var output = Enumerable.Repeat(Sentinel, 73).ToArray();
        var partial = false;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (!output[0].Success || output[0].Number != 0) return 0;
            partial |= output[64].Number == 77;
            if (!deadline) source.Cancel();
            return deadline ? 101 : 0;
        });
        var regex = new Utf8Pcre2Regex(string.Concat(Enumerable.Repeat("(a)", 70)), matchTimeout: TimeSpan.FromMilliseconds(100));
        var input = Encoding.UTF8.GetBytes(new string('a', 70));
        var working = new Account();
        if (deadline) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() =>
            regex.Match(input, captures: output, cancellationToken: source.Token, workingMemoryAllowance: working)).ErrorKind);
        else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Match(input, captures: output, cancellationToken: source.Token, workingMemoryAllowance: working)).CancellationToken);
        Assert.True(partial); Assert.Equal(0UL, working.Used);
        foreach (var slot in output.AsSpan(0, 71)) Assert.Equal(default, slot);
        Assert.Equal(Sentinel, output[71]); Assert.Equal(Sentinel, output[72]);
    }

    private static Utf8Pcre2CaptureRange Sentinel => new(77, true, 99, 100, true);
    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used, Maximum = 64 << 20;
        internal int Attempts;
        internal Action? AfterGrant;
        public bool TryReserve(ulong bytes)
        {
            Attempts++; if (bytes > Maximum - Math.Min(Maximum, Used)) return false;
            Used += bytes; AfterGrant?.Invoke(); return true;
        }
        public void Release(ulong bytes) { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}

using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FiniteLiteralCountTests
{
    private static Utf8Regex Create(string pattern, ulong work, RegexOptions options)
        => new(pattern, options | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = work, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });

    [Theory]
    [InlineData("aa|a", "aaa", 2)]
    [InlineData("a|aa", "aaa", 3)]
    [InlineData("abcdefgh|xyzzz|ztzzz", "abcde xyzzz ztzzz abcdefgh", 3)]
    [InlineData("😀😀|😀|中", "😀😀😀中", 3)]
    [InlineData("😀|😀😀|中", "😀😀😀中", 4)]
    [InlineData("(a)|(bc)|(def)", "adefbca", 4)]
    public void SmallOrderedFamiliesCountWithoutVmStorage(string pattern, string text, int expected)
    {
        var storage = Encoding.UTF8.GetBytes("padding" + text + "padding");
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(pattern, 100000, mode);
            Assert.Equal(expected, regex.Count(storage.AsSpan(7, Encoding.UTF8.GetByteCount(text))));
        }
    }

    [Theory]
    [InlineData("aa|a", "aaa")]
    [InlineData("😀😀|😀|中", "😀😀😀中")]
    public void SmallFamilyWorkDenialIsCumulativeAndNeverPublishesPartialCount(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Utf8Regex(pattern).Count(input);
        ulong low = 1, high = 100000;
        while (low < high)
        {
            var cap = low + (high - low) / 2;
            try { Assert.Equal(expected, Create(pattern, cap, RegexOptions.Compiled).Count(input)); high = cap; }
            catch (Utf8ResourceLimitExceededException failure)
            {
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
                Assert.Equal(cap + 1, failure.Requested);
                low = cap + 1;
            }
        }
        for (ulong cap = 1; cap < low; cap++)
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                Create(pattern, cap, RegexOptions.Compiled).Count(input));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
            Assert.Equal(cap + 1, failure.Requested);
        }
        Assert.Equal(expected, Create(pattern, low, RegexOptions.Compiled).Count(input));
    }

    [Fact]
    public void SmallFamilyCountStillRejectsMalformedSuffix()
    {
        var regex = Create("ab|cd|ef", 100000, RegexOptions.Compiled);
        byte[] input = [.. "ab cd"u8, 0xF0, 0x9F];
        Assert.Throws<ArgumentException>(() => regex.Count(input));
    }

    [Theory]
    [InlineData("abcdefgh|xyzzz|ztzzz", "abcde xyzzz ztzzz abcdefgh", 3)]
    [InlineData("abcde|abcd|abcz", "abcde abcd abcz", 3)]
    [InlineData("😀😀|😀|中", "😀😀😀中", 3)]
    public void SmallFamilyWindowsKeepPriorityAndCompleteScalarVerification(string pattern, string hits, int expected)
    {
        foreach (var prefixLength in new[] { 2047, 2048, 2049, 4097 })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var text = new string('a', prefixLength) + "é😃" + hits + "😃x";
            var storage = Encoding.UTF8.GetBytes("padding" + text + "padding");
            var input = storage.AsSpan(7, Encoding.UTF8.GetByteCount(text));
            var baseline = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant);
            var limited = Create(pattern, 1_000_000, options);
            Assert.Equal(expected, baseline.Count(input));
            Assert.Equal(expected, limited.Count(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, limited.Count(input, source.Token));
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(15)] [InlineData(16)]
    [InlineData(17)] [InlineData(31)] [InlineData(2047)]
    [InlineData(2048)] [InlineData(2049)] [InlineData(4097)]
    public void BoundedLiteralCountPreservesFalseCandidatesChunkEdgesAndBorrowedSlices(int length)
    {
        var literal = new string('a', length - 1) + "b";
        var regex = Create(literal, 1000000, RegexOptions.Compiled);
        foreach (var prefix in new[] { 0, 2032, 2033, 2034, 2047, 2048, 2049 })
        {
            var subject = "😀é" + new string('a', prefix) + literal + " " + literal + "a";
            var storage = Encoding.UTF8.GetBytes("padding" + subject + "padding");
            var input = storage.AsSpan(7, Encoding.UTF8.GetByteCount(subject));
            Assert.Equal(2, regex.Count(input));
            Assert.Equal(2, new Utf8Regex(literal).Count(input));
        }
    }

    [Theory]
    [InlineData("ab", "é😀ab ab", 2)]
    [InlineData("(a)(b)", "ab ab ab", 3)]
    [InlineData("😀é", "😀é 😀é", 2)]
    [InlineData("(\\.)", "...", 3)]
    [InlineData("aaaa", "aaaaaaa", 1)]
    [InlineData("absent", "é😀", 0)]
    [InlineData("CancellationToken", "é😀CancellationToken CancellationToken", 2)]
    public void ExactLiteralsAndOrdinaryCapturesNeedNoVmStorage(string pattern, string text, int expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(pattern, 100000, mode);
            Assert.Equal(expected, regex.Count(input));
            Assert.Equal(expected, regex.Count(input, source.Token));
            Assert.Equal(expected, regex.Count(input));
        }
    }

    [Theory]
    [InlineData("(a)(b)")]
    [InlineData("😀é")]
    public void WorkAccumulatesAcrossValidationEveryHitAndTerminalMiss(string pattern)
    {
        var text = pattern.Contains("😀", StringComparison.Ordinal) ? "😀é 😀é 😀é" : "ab ab ab";
        var input = Encoding.UTF8.GetBytes(text);
        ulong low = 1, high = 100000;
        while (low < high)
        {
            var cap = low + (high - low) / 2;
            try { Assert.Equal(3, Create(pattern, cap, RegexOptions.Compiled).Count(input)); high = cap; }
            catch (Utf8ResourceLimitExceededException failure)
            {
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
                Assert.Equal(cap, failure.Limit); Assert.Equal(cap + 1, failure.Requested); low = cap + 1;
            }
        }
        for (ulong cap = 1; cap < low; cap++)
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(pattern, cap, RegexOptions.Compiled).Count(input));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
            Assert.Equal(cap, failure.Limit); Assert.Equal(cap + 1, failure.Requested);
        }
        var admitted = Create(pattern, low, RegexOptions.Compiled);
        Assert.Equal(3, admitted.Count(input)); Assert.Equal(3, admitted.Count(input));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OffsetEntryKeepsScalarCoordinatesAndFundedMap(bool compiled)
    {
        var input = "é😀ababxabab"u8.ToArray();
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var regex = new Utf8Regex("(a)(b)", options, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 });
        var baseline = new Utf8Regex("(a)(b)", options);
        foreach (var offset in new[] { 0, 1, 3, 4, 5, 7, 9 })
            Assert.Equal(baseline.CountFromUtf16Offset(input, offset), regex.CountFromUtf16Offset(input, offset));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.CountFromUtf16Offset(input, 2));
        var tight = new Utf8Regex("(a)(b)", options, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1_048_576 });
        Assert.Equal(4, tight.Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => tight.CountFromUtf16Offset(input, 0)).ResourceKind);
    }

    [Theory]
    [InlineData("(a)(b)", false, false)] [InlineData("(a)(b)", false, true)]
    [InlineData("(a)(b)", true, false)] [InlineData("(a)(b)", true, true)]
    [InlineData("ab|cd|ef", false, false)] [InlineData("ab|cd|ef", false, true)]
    [InlineData("ab|cd|ef", true, false)] [InlineData("ab|cd|ef", true, true)]
    public void OriginalInterruptionCoversValidationAndNativeSearch(string pattern, bool validationPhase, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var input = new byte[320000]; Array.Fill(input, (byte)'x');
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled,
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, policy);
        var probe = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref probe);
        var validationSteps = probe.Budget.Steps;
        var triggered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (triggered || !validationPhase && steps <= validationSteps) return;
            triggered = true;
            if (expire) clock.Advance(250); else source.Cancel();
        });
        try
        {
            if (expire) Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, source.Token));
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token)).CancellationToken);
            Assert.True(triggered);
        }
        finally { diagnostics.Complete(); }
    }

    [Fact]
    public void MalformedTailNeverBecomesAnEarlySuccessfulCount()
    {
        var regex = Create("(a)(b)", 100000, RegexOptions.None);
        byte[] input = [.. "ab ab"u8, 0xF0, 0x9F];
        Assert.Throws<ArgumentException>(() => regex.Count(input));
        Assert.Throws<ArgumentException>(() => regex.CountFromUtf16Offset(input, 0));
    }

    [Theory]
    [InlineData("(?<x>😀)(é)", "😀é 😀é", RegexOptions.None)]
    [InlineData("(a)(b)", "ab ab", RegexOptions.NonBacktracking)]
    [InlineData("a*", "aa", RegexOptions.None)]
    [InlineData(@"\bab\b", "abxab ab", RegexOptions.None)]
    [InlineData(@"\Gab", "ababxabab", RegexOptions.None)]
    [InlineData("ab", "ababxab", RegexOptions.RightToLeft)]
    [InlineData("(?<=x)(a)(b)", "xab ab xab", RegexOptions.None)]
    [InlineData(@"(a)(b)\1", "aba ab abb", RegexOptions.None)]
    [InlineData("(?<s>a)(?<s>b)", "ab ab", RegexOptions.None)]
    [InlineData("(?<1>a)(b)", "ab ab", RegexOptions.None)]
    [InlineData("^(a)(b)$", "ab", RegexOptions.None)]
    [InlineData("^(aa|bb)$", "xaa aa bb bbx", RegexOptions.None)]
    [InlineData(@"\b(?:aa|ab)\b", "xaa ab aa abx", RegexOptions.None)]
    [InlineData("aa|a", "aaa", RegexOptions.RightToLeft)]
    [InlineData("aa|a", "AAA", RegexOptions.IgnoreCase)]
    [InlineData("|ab", "😀ab", RegexOptions.None)]
    [InlineData(@"(aa|bb)\1", "aaaa aa bb bbbb", RegexOptions.None)]
    [InlineData("(?<x>aa)|(?<x>bb)", "aa bb", RegexOptions.None)]
    [InlineData("(?<1>aa)|bb", "aa bb", RegexOptions.None)]
    [InlineData("a|aa", "aaa", RegexOptions.NonBacktracking)]
    public void UnqualifiedShapesKeepTheirScalarOutcomes(string pattern, string text, RegexOptions mode)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var options = mode | RegexOptions.CultureInvariant;
        var baseline = new Utf8Regex(pattern, options);
        var limited = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 1024, MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 });
        Assert.Equal(baseline.Count(input), limited.Count(input));
        var offset = text.StartsWith("😀", StringComparison.Ordinal) ? 2 : 1;
        Assert.Equal(baseline.CountFromUtf16Offset(input, offset), limited.CountFromUtf16Offset(input, offset));
    }
}

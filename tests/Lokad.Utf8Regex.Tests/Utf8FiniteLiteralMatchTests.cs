using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FiniteLiteralMatchTests
{
    private static Utf8Regex Create(string pattern, ulong work = 100000, RegexOptions options = RegexOptions.None)
        => new(pattern, options | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = work, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });

    private static void AssertRange(Utf8ValueMatch actual, string text, int start, int length)
    {
        Assert.True(actual.Success);
        Assert.True(actual.IsByteAligned);
        Assert.Equal(start, actual.IndexInUtf16);
        Assert.Equal(length, actual.LengthInUtf16);
        Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), actual.IndexInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(start, length)), actual.LengthInBytes);
    }

    [Theory]
    [InlineData("needle", "é😀needle rest", 3, 6)]
    [InlineData("(nee)(dle)", "é😀needle rest", 3, 6)]
    [InlineData("😀é", "x😀é y😀é", 1, 3)]
    [InlineData("aa|a", "xaa", 1, 2)]
    [InlineData("a|aa", "xaa", 1, 1)]
    [InlineData("😀😀|😀|中", "x😀😀中", 1, 4)]
    [InlineData("😀|😀😀|中", "x😀😀中", 1, 2)]
    [InlineData("(a)|(bc)|(def)", "😀zbc", 3, 2)]
    [InlineData("\0", "😀\0x", 2, 1)]
    public void ExactValuesKeepScalarRangesAndOrdinalPriorityWithoutVmStorage(string pattern, string text, int start, int length)
    {
        var input = Encoding.UTF8.GetBytes("padding" + text + "padding");
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(pattern, options: options);
            using var source = new CancellationTokenSource();
            var slice = input.AsSpan(7, Encoding.UTF8.GetByteCount(text));
            AssertRange(regex.Match(slice), text, start, length);
            AssertRange(regex.Match(slice, source.Token), text, start, length);
            Assert.True(regex.IsMatch(slice));
            Assert.True(regex.IsMatch(slice, source.Token));
        }
    }

    [Theory]
    [InlineData(17)] [InlineData(2049)] [InlineData(8193)]
    public void LongLiteralsKeepBoundedComparisonsAfterFalseStarts(int length)
    {
        var literal = new string('a', length - 1) + "b";
        var text = "é😀" + new string('a', 2049) + literal;
        var storage = Encoding.UTF8.GetBytes("padding" + text + "padding");
        using var source = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(literal, options: options);
            var input = storage.AsSpan(7, Encoding.UTF8.GetByteCount(text));
            AssertRange(regex.Match(input, source.Token), text, 2052, length);
            Assert.True(regex.IsMatch(input, source.Token));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OffsetMapsRemainFundedAndDetailedCapturesKeepTheirOwnAdmission(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var regex = new Utf8Regex("(nee)(?<suffix>dle)", options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 100000, MaxDepth = 32, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        const string text = "é😀needle needle";
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        foreach (var offset in new[] { 0, 1, 3, 4, 9, 10, text.Length })
        {
            var expected = new Regex(regex.Pattern, options).Match(text, offset);
            var actual = regex.MatchFromUtf16Offset(input, offset, source.Token);
            Assert.Equal(expected.Success, actual.Success);
            if (expected.Success) AssertRange(actual, text, expected.Index, expected.Length);
            Assert.Equal(expected.Success, regex.IsMatchFromUtf16Offset(input, offset, source.Token));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchFromUtf16Offset(input, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatchFromUtf16Offset(input, 2));
        var detailed = regex.MatchDetailed(input, source.Token);
        Assert.Equal("nee", detailed.GetGroup(1).GetValueString());
        Assert.Equal("dle", detailed.GetGroup("suffix").GetValueString());

        var tiny = Create(regex.Pattern, options: options);
        AssertRange(tiny.Match(input), text, 3, 6);
        foreach (var working in new[] { true, false })
        {
            var limited = new Utf8Regex(regex.Pattern, options, Regex.InfiniteMatchTimeout,
                new() { MaxWorkSteps = 100000, MaxDepth = 32,
                    MaxWorkingBytes = working ? 1UL : 1UL << 20,
                    MaxResultBytes = working ? 1UL << 20 : 1UL });
            var expected = working ? Utf8ResourceLimitKind.ExecutionWorkingBytes : Utf8ResourceLimitKind.ExecutionResultBytes;
            Assert.Equal(expected, Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.MatchDetailed(input)).ResourceKind);
            Assert.Equal(expected, Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.MatchFromUtf16Offset(input, 0)).ResourceKind);
            Assert.Equal(expected, Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.IsMatchFromUtf16Offset(input, 0)).ResourceKind);
        }
    }

    [Theory]
    [InlineData("needle")] [InlineData("needle|other|third")]
    public void WorkDenialIncludesValidationDiscoveryProjectionAndPublication(string pattern)
    {
        var text = new string('é', 4097) + "😀needle";
        var input = Encoding.UTF8.GetBytes(text);
        ulong low = 1, high = 100000;
        while (low < high)
        {
            var cap = low + (high - low) / 2;
            try { Assert.True(Create(pattern, cap).Match(input).Success); high = cap; }
            catch (Utf8ResourceLimitExceededException failure)
            {
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
                Assert.Equal(cap + 1, failure.Requested);
                low = cap + 1;
            }
        }
        for (ulong cap = 1; cap < low; cap++)
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(pattern, cap).Match(input));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
            Assert.Equal(cap, failure.Limit);
            Assert.Equal(cap + 1, failure.Requested);
        }
        AssertRange(Create(pattern, low).Match(input), text, 4099, 6);
    }

    [Theory]
    [InlineData("validation", false)] [InlineData("validation", true)]
    [InlineData("search", false)] [InlineData("search", true)]
    [InlineData("projection", false)] [InlineData("projection", true)]
    public void EveryPhaseKeepsTheOriginalCancellationAndAbsoluteDeadline(string phase, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex("needle", RegexOptions.CultureInvariant,
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes(new string('é', 160000) + "needle");
        var prepared = (Utf8PreparedRegex)typeof(Utf8Regex)
            .GetProperty("PreparedRegex", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(regex)!;
        var probe = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref probe);
        var validationSteps = probe.Budget.Steps;
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindNativeLiteralMatch(prepared.SearchPlan,
            input, 0, input.Length, false, ref probe, out var selected, out var found));
        Assert.True(found);
        Assert.Equal(input.Length - 6, selected.Index);
        var discoverySteps = probe.Budget.Steps;
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || phase == "search" && steps <= validationSteps ||
                phase == "projection" && steps <= discoverySteps) return;
            entered = true;
            if (expire) clock.Advance(250); else source.Cancel();
        });
        try
        {
            if (expire)
            {
                var failure = Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, source.Token));
                Assert.Equal(regex.Pattern, failure.Pattern);
                Assert.Equal(regex.MatchTimeout, failure.MatchTimeout);
            }
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(input, source.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
        Assert.True(regex.Match("needle"u8).Success);
    }

    [Fact]
    public void EarlyHitsStillValidateTheWholeSubjectAndObserveCanceledTokensFirst()
    {
        var regex = Create("needle");
        byte[] invalid = [.. "needle"u8, 0x80];
        Assert.Throws<ArgumentException>(() => regex.Match(invalid));
        Assert.Throws<ArgumentException>(() => regex.IsMatch(invalid));
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(invalid, source.Token)).CancellationToken);
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(invalid, source.Token)).CancellationToken);
        Assert.False(regex.Match("unrelated"u8).Success);
        Assert.False(regex.Match([]).Success);
        Assert.False(regex.IsMatch("unrelated"u8));
        Assert.False(regex.IsMatch([]));
    }

    [Theory]
    [InlineData(@"\bneedle\b", "xneedle needle", RegexOptions.None)]
    [InlineData(@"\Gneedle", "xneedle", RegexOptions.None)]
    [InlineData(@"(?<=x)needle", "xneedle", RegexOptions.None)]
    [InlineData(@"needle(?=!)", "needle needle!", RegexOptions.None)]
    [InlineData(@"(a)\1", "a aa", RegexOptions.None)]
    [InlineData(@"(?<s>a)(?<s>b)", "xab", RegexOptions.None)]
    [InlineData(@"(?<1>a)(b)", "xab", RegexOptions.None)]
    [InlineData(@"^needle$", "xneedle", RegexOptions.None)]
    [InlineData("a*", "😀aaa", RegexOptions.None)]
    [InlineData("|needle", "😀needle", RegexOptions.None)]
    [InlineData("needle", "needle😀needle", RegexOptions.RightToLeft)]
    [InlineData("NEEDLE", "xneedle", RegexOptions.IgnoreCase)]
    [InlineData("a|aa", "xaa", RegexOptions.NonBacktracking)]
    public void ExcludedShapesRetainTheirQualifiedWholeMatch(string pattern, string text, RegexOptions mode)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { mode | RegexOptions.CultureInvariant, mode | RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
                new() { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
            var expected = new Regex(pattern, options).Match(text);
            var actual = regex.Match(input);
            Assert.Equal(expected.Success, actual.Success);
            if (expected.Success) AssertRange(actual, text, expected.Index, expected.Length);
            Assert.Equal(expected.Success, regex.IsMatch(input));
        }
    }

    [Theory]
    [InlineData("(nee)(dle)")] [InlineData("needle|other|third")]
    public void BooleanWorkDenialIncludesTheSuccessfulAndTerminalChecks(string pattern)
    {
        var input = Encoding.UTF8.GetBytes(new string('é', 4097) + "😀needle");
        ulong low = 1, high = 100000;
        while (low < high)
        {
            var cap = low + (high - low) / 2;
            try { Assert.True(Create(pattern, cap).IsMatch(input)); high = cap; }
            catch (Utf8ResourceLimitExceededException failure)
            {
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
                Assert.Equal(cap + 1, failure.Requested);
                low = cap + 1;
            }
        }
        for (ulong cap = 1; cap < low; cap++)
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(pattern, cap).IsMatch(input));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
            Assert.Equal(cap, failure.Limit);
            Assert.Equal(cap + 1, failure.Requested);
        }
        Assert.True(Create(pattern, low).IsMatch(input));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void BooleanValidationAndSearchKeepOriginalInterruption(bool searchPhase, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex("needle", RegexOptions.CultureInvariant,
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes(new string('é', 160000) + "needle");
        var probe = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref probe);
        var validationSteps = probe.Budget.Steps;
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || searchPhase && steps <= validationSteps) return;
            entered = true;
            if (expire) clock.Advance(250); else source.Cancel();
        });
        try
        {
            if (expire) Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input, source.Token));
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, source.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
        Assert.True(regex.IsMatch("needle"u8));
    }
}

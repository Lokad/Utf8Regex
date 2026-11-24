using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// Also run this fixture with IsDynamicCodeSupported disabled in the test host's
// runtimeconfig. No alternate library build or execution mode is required.
public sealed class Utf8DynamicCodeCapabilityTests
{
    [Theory]
    [InlineData(@"\A(?<unit>[1A-Za-z]{2})(?:[QX]?[XYZ]|[uV][0-9])$", "a1X\n", 3)]
    [InlineData(@"\A(?<unit>[1A-Za-z]{2})(?:[QX]?[XYZ]|[uV][0-9])$", "a1u4\n", 4)]
    [InlineData(@"\A(?<unit>[1A-Za-z]{2})[QX]??[XYZ]", "a1XX", 3)]
    [InlineData(@"\A(?<unit>[1A-Za-z]{2})[!07]\z", "A17", 3)]
    public void EmittedAbsoluteProgramKeepsOrderedEndpointsAndCaptures(string pattern, string text, int end)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);
        var bytes = Encoding.UTF8.GetBytes(text);
        var expected = new Utf8CaptureRange(0, end);
        Assert.True(regex.IsMatch(bytes));
        Assert.Equal(expected, regex.Match(bytes));
        Assert.False(regex.IsMatch("éaXX"u8));
        Assert.False(regex.IsMatch("a1é"u8));
        Assert.False(regex.IsMatch("a1w"u8));
        Assert.False(regex.IsMatch("a1\u0018"u8));
        var slots = new Utf8CaptureRange[2];
        Assert.Equal(expected, regex.Match(bytes, captures: slots));
        Assert.Equal(new Utf8CaptureRange(0, 2), slots[1]);
        using var cursor = regex.CreateMatchCursor(bytes);
        Assert.True(cursor.MoveNext(out var retained));
        Assert.Equal(expected, retained.Value);
        Assert.Equal(slots[1], Assert.Single(retained.GetCaptures(1).ToArray()));
    }

    [Theory]
    [InlineData("a12", 1)]
    [InlineData("Ab1 2", 2)]
    [InlineData("aX", 0)]
    [InlineData("Abc12", -1)]
    [InlineData("Aé12", -1)]
    [InlineData("a1  2", -1)]
    public void EmittedSingleEndpointRepeatKeepsTheOriginalForkOrigin(string text, int letters)
    {
        using var regex = new Utf8Regex(@"\A(?:(?<letters>[A-Za-z]{1,2})[0-9] ?[0-9]|[A-Za-z]X)\z",
            RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(letters >= 0, regex.IsMatch(input));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.Equal(letters >= 0, cursor.MoveNext(out var match));
        if (letters < 0) return;
        Assert.Equal(new Utf8CaptureRange(0, input.Length), match.Value);
        if (letters == 0) Assert.Empty(match.GetCaptures(1).ToArray());
        else Assert.Equal(new Utf8CaptureRange(0, letters), Assert.Single(match.GetCaptures(1).ToArray()));
    }

    [Theory]
    [InlineData("255", 0, 3, 1)]
    [InlineData("é😀:00 200 255 099", 7, 9, 4)]
    [InlineData("2é255", 3, 6, 1)]
    [InlineData("!1é2😀", -1, -1, 0)]
    public void EmittedCandidateProgramReturnsAbsoluteByteOffsets(string text, int start, int end, int count)
    {
        using var regex = new Utf8Regex(@"(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])", RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(count > 0, regex.IsMatch(input));
        Assert.Equal(count, regex.Count(input));
        var match = regex.Match(input);
        Assert.Equal(count > 0, match.Success);
        if (match.Success) Assert.Equal(new Utf8CaptureRange(start, end), match);
    }

    [Theory]
    [InlineData("012.200.033.199", 0, 15)]
    [InlineData("é😀:3256.33.44.0559", 9, 21)]
    [InlineData("2é:01.02.03.04", 4, 15)]
    [InlineData("012.200.033.1AA", -1, -1)]
    [InlineData("25é.01.02.03", -1, -1)]
    public void EmittedDelimitedAlternativesKeepCandidateOrder(string text, int start, int end)
    {
        const string octet = "(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        using var regex = new Utf8Regex(@"(?:" + octet + @"\.){3}" + octet, RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(start >= 0, regex.IsMatch(input));
        Assert.Equal(start < 0 ? 0 : 1, regex.Count(input));
        var match = regex.Match(input);
        Assert.Equal(start >= 0, match.Success);
        if (match.Success) Assert.Equal(new Utf8CaptureRange(start, end), match);
    }

    [Fact]
    public void EmittedDecimalRunsKeepByteRangesAndRepeatedCaptures()
    {
        using var regex = new Utf8Regex(@"(\d{4}[- ]){3}\d{3,4}", RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        const string prefix = "😀:";
        const string first = "𝟙2٣4-";
        const string value = first + "5678-1234-𝟜5٦";
        var input = Encoding.UTF8.GetBytes(prefix + value + "!");
        var start = Encoding.UTF8.GetByteCount(prefix);
        var end = start + Encoding.UTF8.GetByteCount(value);
        var firstEnd = start + Encoding.UTF8.GetByteCount(first);
        var expected = new Utf8CaptureRange(start, end);
        Assert.True(regex.IsMatch(input));
        Assert.Equal(expected, regex.Match(input));
        Assert.Equal(1, regex.Count(input));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(expected, match.Value);
        Assert.Equal(new Utf8CaptureRange[]
        {
            new(start, firstEnd), new(firstEnd, firstEnd + 5), new(firstEnd + 5, firstEnd + 10)
        }, match.GetCaptures(1).ToArray());
    }

    [Fact]
    public void EmittedUnboundedDecimalRunsRespectTheShortExtent()
    {
        using var regex = new Utf8Regex(@"^[-+]?\d*\.?\d*$", RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        Assert.Equal(new Utf8CaptureRange(0, 0), regex.Match(""u8));
        Assert.Equal(new Utf8CaptureRange(0, 1), regex.Match("+"u8));
        Assert.Equal(new Utf8CaptureRange(0, 2), regex.Match("+."u8));
        Assert.Equal(new Utf8CaptureRange(0, 8), regex.Match("-3.14159\n"u8));
        Assert.Equal(new Utf8CaptureRange(0, 7), regex.Match("𝟛.١\n"u8));
        Assert.False(regex.IsMatch("+.1.2"u8));
        Assert.False(regex.IsMatch("٠.²"u8));

        foreach (var length in new[] { 31, 32, 33, 512 })
        {
            var digits = new string('1', length);
            Assert.Equal(new Utf8CaptureRange(0, length), regex.Match(Encoding.UTF8.GetBytes(digits + "\n")));
            Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(digits + "x")));
        }
    }

    [Theory]
    [InlineData("abc|xyz", "é😀 xyz abc", 2)]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "é😀 class Bravo", 1)]
    [InlineData("^[0-9]{4}-[0-9]{2}-[0-9]{2}$", "2026-10-26", 1)]
    [InlineData("^ab[0-9]{2}$", "ab12", 1)]
    [InlineData("(?<x>😀)+\\k<x>", "é😀😀", 1)]
    [InlineData("(?<x>.)", "é😀", 2)]
    public void PublicResultsAndControlsDoNotRequireEmission(string pattern, string text, int count)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        {
            if (pattern.Contains("\\k") && mode is RegexOptions.NonBacktracking or RegexOptions.RightToLeft) continue;
            var options = mode | RegexOptions.CultureInvariant;
            var regex = new Utf8Regex(pattern, options: options);
            Assert.Equal(options, regex.Options);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(count, regex.Count(input));
            var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.True(match.Success);
            Assert.Equal(Encoding.UTF8.GetString(match.GetGroupMemory(0).Span), System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span));
            if (pattern.Contains("?<x>"))
                Assert.Equal(pattern == "(?<x>.)" && mode != RegexOptions.RightToLeft ? "é" : "😀",
                    System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.Equal(canceled.Token,
                Assert.Throws<OperationCanceledException>(() => regex.Count(input, canceled.Token)).CancellationToken);
            var finite = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxWorkSteps = 1_000_000,
                    MaxDepth = 2048,
                    MaxWorkingBytes = 1_048_576,
                    MaxResultBytes = 1_048_576
                }
            });
            Assert.Equal(count, finite.Count(input));
        }
    }

}

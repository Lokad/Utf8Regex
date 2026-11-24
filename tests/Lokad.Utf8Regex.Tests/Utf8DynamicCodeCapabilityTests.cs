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
            program.EmittedAnchoredMatch is not null);
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
